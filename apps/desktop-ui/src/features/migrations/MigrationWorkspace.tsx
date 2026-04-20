import { useEffect, useMemo, useState } from "react";
import { api } from "../../api/client";
import type { TranslationKey } from "../../i18n";
import type {
	MigrationExecutionRun,
	MigrationItem,
	MigrationStatus,
	SchemaSnapshotSummary,
} from "../../types";

interface Props {
	connectionId: string;
	t: (key: TranslationKey) => string;
}

interface MigrationDraft {
	id: string | null;
	name: string;
	description: string;
	upScript: string;
	downScript: string;
	status: MigrationStatus;
}

const EMPTY_DRAFT: MigrationDraft = {
	id: null,
	name: "",
	description: "",
	upScript: "",
	downScript: "",
	status: "draft",
};

export function MigrationWorkspace({ connectionId, t }: Props) {
	const [migrations, setMigrations] = useState<MigrationItem[]>([]);
	const [history, setHistory] = useState<MigrationExecutionRun[]>([]);
	const [snapshots, setSnapshots] = useState<SchemaSnapshotSummary[]>([]);
	const [draft, setDraft] = useState<MigrationDraft>(EMPTY_DRAFT);
	const [sourceSnapshotId, setSourceSnapshotId] = useState("");
	const [targetSnapshotId, setTargetSnapshotId] = useState("");
	const [isBusy, setIsBusy] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const selectedMigration = useMemo(
		() => migrations.find((item) => item.id === draft.id) ?? null,
		[migrations, draft.id],
	);

	const destructiveWarning = useMemo(
		() => hasDestructiveSql(draft.upScript) || hasDestructiveSql(draft.downScript),
		[draft.upScript, draft.downScript],
	);

	const emptyDownWarning = draft.downScript.trim().length === 0;

	const summary = useMemo(() => {
		const counts: Record<MigrationStatus, number> = {
			draft: 0,
			pending: 0,
			applied: 0,
			failed: 0,
			rolledback: 0,
		};

		for (const item of migrations) {
			counts[item.status] += 1;
		}

		return counts;
	}, [migrations]);

	useEffect(() => {
		void reloadAll();
	}, [connectionId]);

	async function reloadAll() {
		try {
			setError(null);
			const [snapshotsResult, migrationsResult, historyResult] = await Promise.all([
				api.listSnapshots(connectionId),
				api.listMigrations(connectionId),
				api.listMigrationHistoryByConnection(connectionId, 300),
			]);

			setSnapshots(snapshotsResult);
			setMigrations(migrationsResult);
			setHistory(historyResult);

			const latest = snapshotsResult[0];
			const previous = snapshotsResult[1];
			setTargetSnapshotId(latest?.id ?? "");
			setSourceSnapshotId(previous?.id ?? "");

			if (!draft.id && migrationsResult.length > 0) {
				setDraft(toDraft(migrationsResult[0]));
			}
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to load migrations");
		}
	}

	async function handleGenerateFromDiff() {
		if (!sourceSnapshotId || !targetSnapshotId) {
			setError(t("migrationSelectSnapshotsPrompt"));
			return;
		}

		if (sourceSnapshotId === targetSnapshotId) {
			setError(t("schemaDiffSelectDifferentSnapshotsError"));
			return;
		}

		setIsBusy(true);
		setError(null);
		try {
			const generated = await api.generateMigrationFromDiff({
				connectionId,
				sourceSnapshotId,
				targetSnapshotId,
				name: draft.name.trim() || undefined,
				description: draft.description.trim() || undefined,
			});

			await reloadAll();
			setDraft(toDraft(generated));
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to generate migration from diff");
		} finally {
			setIsBusy(false);
		}
	}

	function handleSelectMigration(migration: MigrationItem) {
		setDraft(toDraft(migration));
	}

	function handleNewMigration() {
		setDraft(EMPTY_DRAFT);
	}

	async function handleSaveMigration() {
		if (!draft.name.trim()) {
			setError(`${t("migrationNameLabel")}: required`);
			return;
		}

		if (!draft.upScript.trim()) {
			setError(`${t("migrationUpScriptLabel")}: required`);
			return;
		}

		setIsBusy(true);
		setError(null);
		try {
			const payload = {
				connectionId,
				name: draft.name.trim(),
				description: draft.description.trim() || undefined,
				upScript: draft.upScript,
				downScript: draft.downScript,
				status: inferDraftStatus(draft),
			};

			const saved = draft.id
				? await api.updateMigration(draft.id, payload)
				: await api.saveMigration(payload);

			await reloadAll();
			setDraft(toDraft(saved));
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to save migration");
		} finally {
			setIsBusy(false);
		}
	}

	async function handleDeleteMigration() {
		if (!draft.id) {
			return;
		}

		if (selectedMigration?.status === "applied") {
			setError(t("deleteMigrationBlocked"));
			return;
		}

		if (!window.confirm(t("migrationDeleteConfirm"))) {
			return;
		}

		setIsBusy(true);
		setError(null);
		try {
			await api.deleteMigration(draft.id);
			setDraft(EMPTY_DRAFT);
			await reloadAll();
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to delete migration");
		} finally {
			setIsBusy(false);
		}
	}

	async function handleApplyMigration() {
		if (!draft.id) {
			return;
		}

		if (!window.confirm(t("migrationConfirmApply"))) {
			return;
		}

		const confirmDestructive = destructiveWarning
			? window.confirm(`${t("migrationDestructiveWarning")} Continue?`)
			: false;

		setIsBusy(true);
		setError(null);
		try {
			await api.applyMigration(connectionId, draft.id, confirmDestructive);
			await reloadAll();
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to apply migration");
		} finally {
			setIsBusy(false);
		}
	}

	async function handleRollbackMigration() {
		if (!draft.id) {
			return;
		}

		if (!draft.downScript.trim()) {
			setError(t("migrationDownScriptWarning"));
			return;
		}

		if (!window.confirm(t("migrationConfirmRollback"))) {
			return;
		}

		const confirmDestructive = destructiveWarning
			? window.confirm(`${t("migrationDestructiveWarning")} Continue?`)
			: false;

		setIsBusy(true);
		setError(null);
		try {
			await api.rollbackMigration(connectionId, draft.id, confirmDestructive);
			await reloadAll();
		} catch (err) {
			setError(err instanceof Error ? err.message : "Failed to rollback migration");
		} finally {
			setIsBusy(false);
		}
	}

	const filteredHistory = useMemo(() => {
		if (!draft.id) {
			return history;
		}

		return history.filter((item) => item.migrationId === draft.id);
	}, [history, draft.id]);

	return (
		<div className="card panel-card migration-workspace">
			<div className="card-header">
				<div>
					<p className="eyebrow">{t("migrationWorkspaceTitle")}</p>
					<h3>{t("migrationWorkspaceTitle")}</h3>
					<p className="small">{t("migrationWorkspaceHint")}</p>
				</div>
				<div className="row migration-counters">
					<span className="badge subtle">{t("migrationStatusDraft")}: {summary.draft}</span>
					<span className="badge subtle">{t("migrationStatusPending")}: {summary.pending}</span>
					<span className="badge subtle">{t("migrationStatusApplied")}: {summary.applied}</span>
					<span className="badge subtle">{t("migrationStatusFailed")}: {summary.failed}</span>
				</div>
			</div>

			{error ? <p className="error-banner">{error}</p> : null}

			<div className="migration-layout">
				<div className="migration-list">
					<div className="field">
						<label>{t("sourceSnapshotLabel")}</label>
						<select value={sourceSnapshotId} onChange={(event) => setSourceSnapshotId(event.target.value)}>
							<option value="">-</option>
							{snapshots.map((snapshot) => (
								<option key={`migration-source-${snapshot.id}`} value={snapshot.id}>
									{(snapshot.name || snapshot.id)} · {new Date(snapshot.createdAtUtc).toLocaleString()}
								</option>
							))}
						</select>
					</div>
					<div className="field">
						<label>{t("targetSnapshotLabel")}</label>
						<select value={targetSnapshotId} onChange={(event) => setTargetSnapshotId(event.target.value)}>
							<option value="">-</option>
							{snapshots.map((snapshot) => (
								<option key={`migration-target-${snapshot.id}`} value={snapshot.id}>
									{(snapshot.name || snapshot.id)} · {new Date(snapshot.createdAtUtc).toLocaleString()}
								</option>
							))}
						</select>
					</div>
					<div className="row">
						<button onClick={handleNewMigration}>{t("newMigration")}</button>
						<button onClick={handleGenerateFromDiff} disabled={isBusy || !sourceSnapshotId || !targetSnapshotId}>
							{t("generateFromDiff")}
						</button>
					</div>
					{migrations.length === 0 ? (
						<p className="small">{t("migrationsEmpty")}</p>
					) : (
						<div className="list">
							{migrations.map((item) => (
								<button
									key={item.id}
									type="button"
									className={`connection-item ${draft.id === item.id ? "active" : ""}`}
									onClick={() => handleSelectMigration(item)}
								>
									<span className="connection-name">{item.name}</span>
									<span className="small">{formatStatus(item.status, t)}</span>
								</button>
							))}
						</div>
					)}
				</div>

				<div className="migration-editor stack">
					{!draft.id && !draft.name && !draft.upScript ? (
						<p className="small">{t("migrationSelectPrompt")}</p>
					) : null}

					<div className="field">
						<label>{t("migrationNameLabel")}</label>
						<input
							value={draft.name}
							onChange={(event) => setDraft((current) => ({ ...current, name: event.target.value }))}
						/>
					</div>
					<div className="field">
						<label>{t("migrationDescriptionLabel")}</label>
						<input
							value={draft.description}
							onChange={(event) => setDraft((current) => ({ ...current, description: event.target.value }))}
						/>
					</div>
					<div className="field">
						<label>{t("migrationUpScriptLabel")}</label>
						<textarea
							className="migration-script"
							value={draft.upScript}
							onChange={(event) => setDraft((current) => ({ ...current, upScript: event.target.value }))}
						/>
					</div>
					<div className="field">
						<label>{t("migrationDownScriptLabel")}</label>
						<textarea
							className="migration-script"
							value={draft.downScript}
							onChange={(event) => setDraft((current) => ({ ...current, downScript: event.target.value }))}
						/>
					</div>

					{emptyDownWarning ? <p className="small warning-text">{t("migrationDownScriptWarning")}</p> : null}
					{destructiveWarning ? <p className="small warning-text">{t("migrationDestructiveWarning")}</p> : null}

					<div className="row">
						<button className="primary-button" onClick={handleSaveMigration} disabled={isBusy}>{t("saveMigration")}</button>
						<button onClick={handleApplyMigration} disabled={isBusy || !draft.id}>{t("applyMigration")}</button>
						<button onClick={handleRollbackMigration} disabled={isBusy || !draft.id}>{t("rollbackMigration")}</button>
						<button onClick={handleDeleteMigration} disabled={isBusy || !draft.id}>{t("deleteSavedQuery")}</button>
					</div>

					<div className="card panel-card">
						<div className="card-header">
							<div>
								<p className="eyebrow">{t("migrationHistoryTitle")}</p>
								<h3>{t("migrationHistoryTitle")}</h3>
							</div>
							<span className="badge subtle">{filteredHistory.length}</span>
						</div>
						{filteredHistory.length === 0 ? (
							<p className="small">-</p>
						) : (
							<div className="list">
								{filteredHistory.map((item) => (
									<div key={item.migrationRunId} className={`list-item-card compact ${item.status === "failed" ? "breaking-change" : ""}`}>
										<strong>{item.direction === "up" ? t("migrationDirectionUp") : t("migrationDirectionDown")} · {item.status === "succeeded" ? t("migrationRunSucceeded") : t("migrationRunFailed")}</strong>
										<p className="small">{new Date(item.executedAtUtc).toLocaleString()} · {item.durationMs} {t("msLabel")}</p>
										{item.executionLog ? <p className="small truncate-line">{item.executionLog}</p> : null}
										{item.errorMessage ? <p className="small">{item.errorMessage}</p> : null}
									</div>
								))}
							</div>
						)}
					</div>
				</div>
			</div>
		</div>
	);
}

function toDraft(migration: MigrationItem): MigrationDraft {
	return {
		id: migration.id,
		name: migration.name,
		description: migration.description ?? "",
		upScript: migration.upScript,
		downScript: migration.downScript,
		status: migration.status,
	};
}

function inferDraftStatus(draft: MigrationDraft): MigrationStatus {
	if (!draft.upScript.trim() || !draft.name.trim()) {
		return "draft";
	}

	if (!draft.downScript.trim()) {
		return "draft";
	}

	if (draft.status === "applied" || draft.status === "rolledback") {
		return draft.status;
	}

	return "pending";
}

function hasDestructiveSql(sql: string): boolean {
	const normalized = ` ${sql.toLowerCase()} `;
	const patterns = [
		" drop table ",
		" drop column ",
		" truncate ",
		" delete from ",
		" alter table ",
	];

	return patterns.some((pattern) => normalized.includes(pattern));
}

function formatStatus(status: MigrationStatus, t: (key: TranslationKey) => string): string {
	switch (status) {
		case "draft":
			return t("migrationStatusDraft");
		case "pending":
			return t("migrationStatusPending");
		case "applied":
			return t("migrationStatusApplied");
		case "failed":
			return t("migrationStatusFailed");
		case "rolledback":
			return t("migrationStatusRolledBack");
		default:
			return status;
	}
}
