using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using SchemaForge.Domain;
using System.Data;

namespace SchemaForge.Infrastructure;

public static class ConnectionStringFactory
{
    public static string Build(SavedConnection connection)
    {
        return connection.DatabaseType switch
        {
            DatabaseType.Postgres => new NpgsqlConnectionStringBuilder
            {
                Host = connection.Host,
                Port = connection.Port,
                Database = connection.Database,
                Username = connection.Username,
                Password = connection.Password,
                TrustServerCertificate = true,
            }.ConnectionString,
            DatabaseType.MySql => new MySqlConnectionStringBuilder
            {
                Server = connection.Host,
                Port = (uint)connection.Port,
                Database = connection.Database,
                UserID = connection.Username,
                Password = connection.Password,
                AllowUserVariables = true,
            }.ConnectionString,
            _ => throw new NotSupportedException($"Database type '{connection.DatabaseType}' is not supported."),
        };
    }

    public static IDbConnection CreateRemoteConnection(SavedConnection connection)
    {
        return connection.DatabaseType switch
        {
            DatabaseType.Postgres => new NpgsqlConnection(Build(connection)),
            DatabaseType.MySql => new MySqlConnection(Build(connection)),
            _ => throw new NotSupportedException($"Database type '{connection.DatabaseType}' is not supported."),
        };
    }

    public static SqliteConnection CreateLocalStorage(string dbPath)
    {
        return new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath }.ConnectionString);
    }
}
