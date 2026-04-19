using Dapper;

namespace SchemaForge.Infrastructure;

internal static class SqliteLocalSchema
{
    private const string AppConnectionsTableSql = @"
create table if not exists app_connections (
    id text primary key,
    name text not null,
    database_type text not null,
    host text not null,
    port integer not null,
    database_name text not null,
    username text not null,
    password text null
);";

    public static Task EnsureConnectionsTableAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        return connection.ExecuteAsync(AppConnectionsTableSql);
    }
}