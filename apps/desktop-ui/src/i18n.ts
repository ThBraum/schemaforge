import type { Language } from "./types";

export const languages: Array<{ value: Language; label: string }> = [
	{ value: "en", label: "English" },
	{ value: "pt-BR", label: "Português (BR)" },
];

export type TranslationKey =
	| "appTitle"
	| "appSubtitle"
	| "themeLabel"
	| "darkThemeLabel"
	| "lightThemeLabel"
	| "languageLabel"
	| "connectionsTitle"
	| "connectionsEmpty"
	| "connectionsEmptyHint"
	| "deleteConnection"
	| "deleteConnectionConfirm"
	| "newConnectionTitle"
	| "connectionFormHint"
	| "nameLabel"
	| "databaseLabel"
	| "postgresqlLabel"
	| "mysqlLabel"
	| "portLabel"
	| "hostLabel"
	| "databaseNameLabel"
	| "usernameLabel"
	| "passwordLabel"
	| "saveConnection"
	| "savingConnection"
	| "explorerTitle"
	| "noConnectionSelected"
	| "connectedTo"
	| "schemasTitle"
	| "structureTitle"
	| "tableColumnHeader"
	| "tableTypeHeader"
	| "tableNullableHeader"
	| "tableDefaultHeader"
	| "previewTitle"
	| "queryRunnerTitle"
	| "runQuery"
	| "queryPlaceholder"
	| "selectedTableEmpty"
	| "previewEmpty"
	| "schemaEmpty"
	| "rowsLabel"
	| "msLabel"
	| "nullLabel"
	| "yes"
	| "no"
	| "savedQueriesTitle"
	| "savedQueriesEmpty"
	| "savedQueryTitleLabel"
	| "savedQueryTagsLabel"
	| "saveCurrentQuery"
	| "editSavedQuery"
	| "newSavedQuery"
	| "openSavedQuery"
	| "deleteSavedQuery"
	| "queryHistoryTitle"
	| "queryHistoryEmpty"
	| "reopenQuery"
	| "snapshotTitle"
	| "captureSnapshot"
	| "snapshotNamePlaceholder"
	| "snapshotsEmpty"
	| "viewSnapshot"
	| "snapshotDetailsTitle"
	| "schemaDiffTitle"
	| "sourceSnapshotLabel"
	| "targetSnapshotLabel"
	| "compareSnapshots"
	| "schemaDiffEmpty"
	| "schemaDiffSummaryTitle"
	| "tablesAddedLabel"
	| "tablesRemovedLabel"
	| "tablesModifiedLabel"
	| "columnsAddedLabel"
	| "columnsRemovedLabel"
	| "columnsModifiedLabel"
	| "breakingChangesLabel"
	| "modifiedTablesLabel"
	| "constraintsChangedLabel"
	| "indexesChangedLabel"
	| "beforeLabel"
	| "afterLabel"
	| "exportJson"
	| "exportHtml"
	| "schemaDiffSelectDifferentSnapshotsError"
	| "schemaDiffSelectSnapshotsForExportError"
	| "migrationWorkspaceTitle"
	| "migrationWorkspaceHint"
	| "migrationsEmpty"
	| "migrationNameLabel"
	| "migrationDescriptionLabel"
	| "migrationUpScriptLabel"
	| "migrationDownScriptLabel"
	| "saveMigration"
	| "newMigration"
	| "applyMigration"
	| "rollbackMigration"
	| "deleteMigrationBlocked"
	| "deleteMigration"
	| "migrationHistoryTitle"
	| "migrationSelectPrompt"
	| "migrationDownScriptWarning"
	| "migrationDestructiveWarning"
	| "migrationConfirmApply"
	| "migrationConfirmRollback"
	| "migrationDeleteConfirm"
	| "migrationStatusDraft"
	| "migrationStatusPending"
	| "migrationStatusApplied"
	| "migrationStatusFailed"
	| "migrationStatusRolledBack"
	| "migrationDirectionUp"
	| "migrationDirectionDown"
	| "migrationRunSucceeded"
	| "migrationRunFailed"
	| "generateFromDiff"
	| "migrationSelectSnapshotsPrompt"
	| "continuePrompt"
	| "migrationNameRequired"
	| "migrationUpScriptRequired"
	| "failedLoadMigrations"
	| "failedGenerateMigrationFromDiff"
	| "failedSaveMigration"
	| "failedDeleteMigration"
	| "failedApplyMigration"
	| "failedRollbackMigration"
	| "failedLoadSchema"
	| "failedLoadPreview"
	| "failedRunQuery"
	| "failedSaveQuery"
	| "failedDeleteQuery"
	| "failedCaptureSnapshot"
	| "failedLoadSnapshot"
	| "failedCompareSnapshots"
	| "failedExportSchemaDiff";

const translations: Record<Language, Record<TranslationKey, string>> = {
	en: {
		appTitle: "SchemaForge",
		appSubtitle: "A modern database explorer for PostgreSQL and MySQL.",
		themeLabel: "Theme",
		darkThemeLabel: "Dark",
		lightThemeLabel: "Light",
		languageLabel: "Language",
		connectionsTitle: "Connections",
		connectionsEmpty: "No saved connections yet.",
		connectionsEmptyHint: "Create one on the left to start exploring your database.",
		deleteConnection: "Delete",
		deleteConnectionConfirm: "Delete this saved connection?",
		newConnectionTitle: "New connection",
		connectionFormHint: "Save credentials locally to connect faster next time.",
		nameLabel: "Connection name",
		databaseLabel: "Database engine",
		postgresqlLabel: "PostgreSQL",
		mysqlLabel: "MySQL",
		portLabel: "Port",
		hostLabel: "Host",
		databaseNameLabel: "Database",
		usernameLabel: "Username",
		passwordLabel: "Password",
		saveConnection: "Save connection",
		savingConnection: "Saving...",
		explorerTitle: "Explorer",
		noConnectionSelected: "Select a connection to begin.",
		connectedTo: "Connected to",
		schemasTitle: "Tables",
		structureTitle: "Table structure",
		tableColumnHeader: "Column",
		tableTypeHeader: "Type",
		tableNullableHeader: "Nullable",
		tableDefaultHeader: "Default",
		previewTitle: "Data preview",
		queryRunnerTitle: "Query runner",
		runQuery: "Run query",
		queryPlaceholder: "select 1 as status;",
		selectedTableEmpty: "No table selected.",
		previewEmpty: "Select a table to preview its rows.",
		schemaEmpty: "No schema loaded yet.",
		rowsLabel: "rows",
		msLabel: "ms",
		nullLabel: "NULL",
		yes: "Yes",
		no: "No",
		savedQueriesTitle: "Saved queries",
		savedQueriesEmpty: "No saved queries yet.",
		savedQueryTitleLabel: "Title",
		savedQueryTagsLabel: "Tags (comma separated)",
		saveCurrentQuery: "Save current query",
		editSavedQuery: "Edit",
		newSavedQuery: "New saved query",
		openSavedQuery: "Open",
		deleteSavedQuery: "Delete",
		queryHistoryTitle: "Query history",
		queryHistoryEmpty: "No query history for this connection yet.",
		reopenQuery: "Reopen",
		snapshotTitle: "Schema snapshots",
		captureSnapshot: "Capture snapshot",
		snapshotNamePlaceholder: "snapshot name (optional)",
		snapshotsEmpty: "No snapshots captured yet.",
		viewSnapshot: "View",
		snapshotDetailsTitle: "Snapshot details",
		schemaDiffTitle: "Schema diff",
		sourceSnapshotLabel: "Source snapshot",
		targetSnapshotLabel: "Target snapshot",
		compareSnapshots: "Compare snapshots",
		schemaDiffEmpty: "Select source and target snapshots to compare.",
		schemaDiffSummaryTitle: "Diff summary",
		tablesAddedLabel: "Tables added",
		tablesRemovedLabel: "Tables removed",
		tablesModifiedLabel: "Tables modified",
		columnsAddedLabel: "Columns added",
		columnsRemovedLabel: "Columns removed",
		columnsModifiedLabel: "Columns modified",
		breakingChangesLabel: "Breaking changes",
		modifiedTablesLabel: "Modified tables",
		constraintsChangedLabel: "Constraints changed",
		indexesChangedLabel: "Indexes changed",
		beforeLabel: "Before",
		afterLabel: "After",
		exportJson: "Export JSON",
		exportHtml: "Export HTML",
		schemaDiffSelectDifferentSnapshotsError: "Select different snapshots to compare.",
		schemaDiffSelectSnapshotsForExportError: "Select source and target snapshots before exporting.",
		migrationWorkspaceTitle: "Migration workspace",
		migrationWorkspaceHint: "Create, review, apply and rollback schema migrations safely.",
		migrationsEmpty: "No migrations for this connection yet.",
		migrationNameLabel: "Migration name",
		migrationDescriptionLabel: "Description (optional)",
		migrationUpScriptLabel: "Up script",
		migrationDownScriptLabel: "Down script",
		saveMigration: "Save migration",
		newMigration: "New migration",
		applyMigration: "Apply",
		rollbackMigration: "Rollback",
		deleteMigrationBlocked: "Applied migrations cannot be deleted.",
		deleteMigration: "Delete",
		migrationHistoryTitle: "Migration execution history",
		migrationSelectPrompt: "Select a migration to edit scripts and metadata.",
		migrationDownScriptWarning: "Down script is empty. Rollback will not be available.",
		migrationDestructiveWarning: "Potentially destructive SQL detected.",
		migrationConfirmApply: "Apply this migration now?",
		migrationConfirmRollback: "Rollback this migration now?",
		migrationDeleteConfirm: "Delete this migration?",
		migrationStatusDraft: "Draft",
		migrationStatusPending: "Pending",
		migrationStatusApplied: "Applied",
		migrationStatusFailed: "Failed",
		migrationStatusRolledBack: "Rolled back",
		migrationDirectionUp: "Up",
		migrationDirectionDown: "Down",
		migrationRunSucceeded: "Succeeded",
		migrationRunFailed: "Failed",
		generateFromDiff: "Generate from diff",
		migrationSelectSnapshotsPrompt: "Select source and target snapshots.",
		continuePrompt: "Continue?",
		migrationNameRequired: "Migration name is required.",
		migrationUpScriptRequired: "Up script is required.",
		failedLoadMigrations: "Failed to load migrations",
		failedGenerateMigrationFromDiff: "Failed to generate migration from diff",
		failedSaveMigration: "Failed to save migration",
		failedDeleteMigration: "Failed to delete migration",
		failedApplyMigration: "Failed to apply migration",
		failedRollbackMigration: "Failed to rollback migration",
		failedLoadSchema: "Failed to load schema",
		failedLoadPreview: "Failed to load preview",
		failedRunQuery: "Failed to run query",
		failedSaveQuery: "Failed to save query",
		failedDeleteQuery: "Failed to delete query",
		failedCaptureSnapshot: "Failed to capture snapshot",
		failedLoadSnapshot: "Failed to load snapshot",
		failedCompareSnapshots: "Failed to compare snapshots",
		failedExportSchemaDiff: "Failed to export schema diff",
	},
	"pt-BR": {
		appTitle: "SchemaForge",
		appSubtitle: "Um explorador moderno de bancos para PostgreSQL e MySQL.",
		themeLabel: "Tema",
		darkThemeLabel: "Escuro",
		lightThemeLabel: "Claro",
		languageLabel: "Idioma",
		connectionsTitle: "Conexões",
		connectionsEmpty: "Ainda não há conexões salvas.",
		connectionsEmptyHint: "Crie uma conexão ao lado para começar a explorar o banco.",
		deleteConnection: "Excluir",
		deleteConnectionConfirm: "Excluir esta conexão salva?",
		newConnectionTitle: "Nova conexão",
		connectionFormHint: "As credenciais ficam salvas localmente para agilizar o próximo acesso.",
		nameLabel: "Nome da conexão",
		databaseLabel: "Motor do banco",
		postgresqlLabel: "PostgreSQL",
		mysqlLabel: "MySQL",
		portLabel: "Porta",
		hostLabel: "Host",
		databaseNameLabel: "Banco de dados",
		usernameLabel: "Usuário",
		passwordLabel: "Senha",
		saveConnection: "Salvar conexão",
		savingConnection: "Salvando...",
		explorerTitle: "Explorador",
		noConnectionSelected: "Selecione uma conexão para começar.",
		connectedTo: "Conectado em",
		schemasTitle: "Tabelas",
		structureTitle: "Estrutura da tabela",
		tableColumnHeader: "Coluna",
		tableTypeHeader: "Tipo",
		tableNullableHeader: "Nulo",
		tableDefaultHeader: "Default",
		previewTitle: "Pré-visualização de dados",
		queryRunnerTitle: "Executor de consultas",
		runQuery: "Executar query",
		queryPlaceholder: "select 1 as status;",
		selectedTableEmpty: "Nenhuma tabela selecionada.",
		previewEmpty: "Selecione uma tabela para ver as linhas.",
		schemaEmpty: "Nenhum schema carregado ainda.",
		rowsLabel: "linhas",
		msLabel: "ms",
		nullLabel: "NULL",
		yes: "Sim",
		no: "Não",
		savedQueriesTitle: "Consultas salvas",
		savedQueriesEmpty: "Nenhuma query salva ainda.",
		savedQueryTitleLabel: "Título",
		savedQueryTagsLabel: "Tags (separadas por vírgula)",
		saveCurrentQuery: "Salvar consulta atual",
		editSavedQuery: "Editar",
		newSavedQuery: "Nova consulta salva",
		openSavedQuery: "Abrir",
		deleteSavedQuery: "Excluir",
		queryHistoryTitle: "Histórico de consultas",
		queryHistoryEmpty: "Ainda não há histórico para esta conexão.",
		reopenQuery: "Reabrir",
		snapshotTitle: "Snapshots do schema",
		captureSnapshot: "Capturar snapshot",
		snapshotNamePlaceholder: "nome do snapshot (opcional)",
		snapshotsEmpty: "Nenhum snapshot capturado ainda.",
		viewSnapshot: "Ver",
		snapshotDetailsTitle: "Detalhes do snapshot",
		schemaDiffTitle: "Diferença de schema",
		sourceSnapshotLabel: "Snapshot de origem",
		targetSnapshotLabel: "Snapshot de destino",
		compareSnapshots: "Comparar snapshots",
		schemaDiffEmpty: "Selecione snapshots de origem e destino para comparar.",
		schemaDiffSummaryTitle: "Resumo do diff",
		tablesAddedLabel: "Tabelas adicionadas",
		tablesRemovedLabel: "Tabelas removidas",
		tablesModifiedLabel: "Tabelas modificadas",
		columnsAddedLabel: "Colunas adicionadas",
		columnsRemovedLabel: "Colunas removidas",
		columnsModifiedLabel: "Colunas modificadas",
		breakingChangesLabel: "Mudanças críticas",
		modifiedTablesLabel: "Tabelas modificadas",
		constraintsChangedLabel: "Constraints alteradas",
		indexesChangedLabel: "Índices alterados",
		beforeLabel: "Antes",
		afterLabel: "Depois",
		exportJson: "Exportar JSON",
		exportHtml: "Exportar HTML",
		schemaDiffSelectDifferentSnapshotsError: "Selecione snapshots diferentes para comparar.",
		schemaDiffSelectSnapshotsForExportError: "Selecione snapshots de origem e destino antes de exportar.",
		migrationWorkspaceTitle: "Área de migrações",
		migrationWorkspaceHint: "Crie, revise, aplique e reverta migrações com segurança.",
		migrationsEmpty: "Nenhuma migração para esta conexão ainda.",
		migrationNameLabel: "Nome da migração",
		migrationDescriptionLabel: "Descrição (opcional)",
		migrationUpScriptLabel: "Script de up",
		migrationDownScriptLabel: "Script de down",
		saveMigration: "Salvar migração",
		newMigration: "Nova migração",
		applyMigration: "Aplicar",
		rollbackMigration: "Rollback",
		deleteMigrationBlocked: "Migrações aplicadas não podem ser excluídas.",
		deleteMigration: "Excluir",
		migrationHistoryTitle: "Histórico de execução de migrações",
		migrationSelectPrompt: "Selecione uma migração para editar scripts e metadados.",
		migrationDownScriptWarning: "Script de down vazio. Rollback não ficará disponível.",
		migrationDestructiveWarning: "SQL potencialmente destrutivo detectado.",
		migrationConfirmApply: "Aplicar esta migração agora?",
		migrationConfirmRollback: "Executar rollback desta migração agora?",
		migrationDeleteConfirm: "Excluir esta migração?",
		migrationStatusDraft: "Rascunho",
		migrationStatusPending: "Pendente",
		migrationStatusApplied: "Aplicada",
		migrationStatusFailed: "Falhou",
		migrationStatusRolledBack: "Revertida",
		migrationDirectionUp: "Up",
		migrationDirectionDown: "Down",
		migrationRunSucceeded: "Sucesso",
		migrationRunFailed: "Falha",
		generateFromDiff: "Gerar do diff",
		migrationSelectSnapshotsPrompt: "Selecione snapshots de origem e destino.",
		continuePrompt: "Continuar?",
		migrationNameRequired: "O nome da migração é obrigatório.",
		migrationUpScriptRequired: "O script de up é obrigatório.",
		failedLoadMigrations: "Falha ao carregar migrações",
		failedGenerateMigrationFromDiff: "Falha ao gerar migração a partir do diff",
		failedSaveMigration: "Falha ao salvar migração",
		failedDeleteMigration: "Falha ao excluir migração",
		failedApplyMigration: "Falha ao aplicar migração",
		failedRollbackMigration: "Falha ao reverter migração",
		failedLoadSchema: "Falha ao carregar schema",
		failedLoadPreview: "Falha ao carregar pré-visualização",
		failedRunQuery: "Falha ao executar consulta",
		failedSaveQuery: "Falha ao salvar consulta",
		failedDeleteQuery: "Falha ao excluir consulta",
		failedCaptureSnapshot: "Falha ao capturar snapshot",
		failedLoadSnapshot: "Falha ao carregar snapshot",
		failedCompareSnapshots: "Falha ao comparar snapshots",
		failedExportSchemaDiff: "Falha ao exportar diferença de schema",
	},
};

export function getTranslation(language: Language) {
	return translations[language];
}
