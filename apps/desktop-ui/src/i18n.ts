import type { Language } from "./types";

export const languages: Array<{ value: Language; label: string }> = [
	{ value: "en", label: "English" },
	{ value: "pt-BR", label: "Português (BR)" },
];

export type TranslationKey =
	| "appTitle"
	| "appSubtitle"
	| "themeLabel"
	| "languageLabel"
	| "connectionsTitle"
	| "connectionsEmpty"
	| "connectionsEmptyHint"
	| "newConnectionTitle"
	| "connectionFormHint"
	| "nameLabel"
	| "databaseLabel"
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
	| "exportHtml";

const translations: Record<Language, Record<TranslationKey, string>> = {
	en: {
		appTitle: "SchemaForge",
		appSubtitle: "A modern database explorer for PostgreSQL and MySQL.",
		themeLabel: "Theme",
		languageLabel: "Language",
		connectionsTitle: "Connections",
		connectionsEmpty: "No saved connections yet.",
		connectionsEmptyHint: "Create one on the left to start exploring your database.",
		newConnectionTitle: "New connection",
		connectionFormHint: "Save credentials locally to connect faster next time.",
		nameLabel: "Connection name",
		databaseLabel: "Database engine",
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
	},
	"pt-BR": {
		appTitle: "SchemaForge",
		appSubtitle: "Um explorador moderno de bancos para PostgreSQL e MySQL.",
		themeLabel: "Tema",
		languageLabel: "Idioma",
		connectionsTitle: "Conexões",
		connectionsEmpty: "Ainda não há conexões salvas.",
		connectionsEmptyHint: "Crie uma conexão ao lado para começar a explorar o banco.",
		newConnectionTitle: "Nova conexão",
		connectionFormHint: "As credenciais ficam salvas localmente para agilizar o próximo acesso.",
		nameLabel: "Nome da conexão",
		databaseLabel: "Motor do banco",
		portLabel: "Porta",
		hostLabel: "Host",
		databaseNameLabel: "Database",
		usernameLabel: "Usuário",
		passwordLabel: "Senha",
		saveConnection: "Salvar conexão",
		savingConnection: "Salvando...",
		explorerTitle: "Explorer",
		noConnectionSelected: "Selecione uma conexão para começar.",
		connectedTo: "Conectado em",
		schemasTitle: "Tabelas",
		structureTitle: "Estrutura da tabela",
		previewTitle: "Preview de dados",
		queryRunnerTitle: "Query runner",
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
		savedQueriesTitle: "Queries salvas",
		savedQueriesEmpty: "Nenhuma query salva ainda.",
		savedQueryTitleLabel: "Título",
		savedQueryTagsLabel: "Tags (separadas por vírgula)",
		saveCurrentQuery: "Salvar query atual",
		editSavedQuery: "Editar",
		newSavedQuery: "Nova query salva",
		openSavedQuery: "Abrir",
		deleteSavedQuery: "Excluir",
		queryHistoryTitle: "Histórico de queries",
		queryHistoryEmpty: "Ainda não há histórico para esta conexão.",
		reopenQuery: "Reabrir",
		snapshotTitle: "Snapshots de schema",
		captureSnapshot: "Capturar snapshot",
		snapshotNamePlaceholder: "nome do snapshot (opcional)",
		snapshotsEmpty: "Nenhum snapshot capturado ainda.",
		viewSnapshot: "Ver",
		snapshotDetailsTitle: "Detalhes do snapshot",
		schemaDiffTitle: "Schema diff",
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
	},
};

export function getTranslation(language: Language) {
	return translations[language];
}
