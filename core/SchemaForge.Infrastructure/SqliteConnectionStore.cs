using Dapper;
using SchemaForge.Application;
using SchemaForge.Domain;
using System.Text.Json;

namespace SchemaForge.Infrastructure;

public sealed class SqliteConnectionStore(string dbPath) : IConnectionStore
{
    public async Task<IReadOnlyCollection<SavedConnection>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var rows = await connection.QueryAsync<ConnectionRow>("select * from app_connections order by name asc");
        return rows.Select(Map).ToArray();
    }

    public async Task<SavedConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        var row = await connection.QuerySingleOrDefaultAsync<ConnectionRow>("select * from app_connections where id = @Id", new { Id = id.ToString() });
        return row is null ? null : Map(row);
    }

    public async Task SaveAsync(SavedConnection connectionModel, CancellationToken cancellationToken = default)
    {
        await using var connection = ConnectionStringFactory.CreateLocalStorage(dbPath);
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection);

        const string sql = @"
insert into app_connections(id, name, database_type, host, port, database_name, username, password)
values(@Id, @Name, @DatabaseType, @Host, @Port, @DatabaseName, @Username, @Password)
on conflict(id) do update set
    name = excluded.name,
    database_type = excluded.database_type,
    host = excluded.host,
    port = excluded.port,
    database_name = excluded.database_name,
    username = excluded.username,
    password = excluded.password;";

        await connection.ExecuteAsync(sql, new
        {
            Id = connectionModel.Id.ToString(),
            connectionModel.Name,
            DatabaseType = connectionModel.DatabaseType.ToString().ToLowerInvariant(),
            connectionModel.Host,
            connectionModel.Port,
            DatabaseName = connectionModel.Database,
            connectionModel.Username,
            connectionModel.Password,
        });
    }

    private static SavedConnection Map(ConnectionRow row)
    {
        return new SavedConnection
        {
            Id = Guid.Parse(row.Id),
            Name = row.Name,
            DatabaseType = row.DatabaseType.ToLowerInvariant() switch
            {
                "postgres" => DatabaseType.Postgres,
                "mysql" => DatabaseType.MySql,
                _ => throw new InvalidOperationException("Unsupported database type in storage."),
            },
            Host = row.Host,
            Port = row.Port,
            Database = row.DatabaseName,
            Username = row.Username,
            Password = row.Password,
        };
    }

    private static async Task EnsureSchemaAsync(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        await SqliteLocalSchema.EnsureConnectionsTableAsync(connection);
    }

    private sealed record ConnectionRow(
        string Id,
        string Name,
        string DatabaseType,
        string Host,
        int Port,
        string DatabaseName,
        string Username,
        string? Password);
}
