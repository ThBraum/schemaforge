using SchemaForge.Application;
using SchemaForge.Infrastructure;
using SchemaForge.Domain;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);
var appDbPath = Path.Combine(dataDir, "schemaforge.db");

builder.Services.AddSingleton<IConnectionStore>(_ => new SqliteConnectionStore(appDbPath));
builder.Services.AddSingleton<IDatabaseProvider, PostgresDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProvider, MySqlDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProviderResolver, DatabaseProviderResolver>();
builder.Services.AddScoped<ExplorerService>();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/connections", async (IConnectionStore store, CancellationToken ct) =>
{
    var items = await store.ListAsync(ct);
    return Results.Ok(items.Select(item => new
    {
        id = item.Id,
        name = item.Name,
        databaseType = item.DatabaseType == DatabaseType.Postgres ? "postgres" : "mysql",
        host = item.Host,
        port = item.Port,
        database = item.Database,
        username = item.Username,
    }));
});

app.MapPost("/api/connections", async (ConnectionRequest request, IConnectionStore store, CancellationToken ct) =>
{
    var connection = new SavedConnection
    {
        Id = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id,
        Name = request.Name,
        DatabaseType = request.DatabaseType.ToLowerInvariant() switch
        {
            "postgres" => DatabaseType.Postgres,
            "mysql" => DatabaseType.MySql,
            _ => throw new InvalidOperationException("Unsupported database type."),
        },
        Host = request.Host,
        Port = request.Port,
        Database = request.Database,
        Username = request.Username,
        Password = request.Password,
    };

    await store.SaveAsync(connection, ct);
    return Results.Ok(new
    {
        id = connection.Id,
        name = connection.Name,
        databaseType = request.DatabaseType,
        host = connection.Host,
        port = connection.Port,
        database = connection.Database,
        username = connection.Username,
    });
});

app.MapGet("/api/explorer/schema/{connectionId:guid}", async (Guid connectionId, ExplorerService service, CancellationToken ct) =>
{
    var result = await service.GetSchemaAsync(connectionId, ct);
    return Results.Ok(new
    {
        connectionId = result.ConnectionId,
        databaseName = result.DatabaseName,
        schemas = result.Schemas.Select(schema => new
        {
            schemaName = schema.SchemaName,
            tables = schema.Tables.Select(table => new
            {
                schemaName = table.SchemaName,
                tableName = table.TableName,
                columns = table.Columns.Select(column => new
                {
                    name = column.Name,
                    dataType = column.DataType,
                    isNullable = column.IsNullable,
                    defaultValue = column.DefaultValue,
                })
            })
        })
    });
});

app.MapGet("/api/explorer/preview/{connectionId:guid}", async (Guid connectionId, string schemaName, string tableName, int? limit, ExplorerService service, CancellationToken ct) =>
{
    var result = await service.PreviewTableAsync(connectionId, schemaName, tableName, limit ?? 100, ct);
    return Results.Ok(new
    {
        schemaName = result.SchemaName,
        tableName = result.TableName,
        columns = result.Columns,
        rows = result.Rows,
    });
});

app.MapPost("/api/query/run/{connectionId:guid}", async (Guid connectionId, QueryRequest request, ExplorerService service, CancellationToken ct) =>
{
    var result = await service.RunQueryAsync(connectionId, request.Sql, ct);
    return Results.Ok(new
    {
        columns = result.Columns,
        rows = result.Rows,
        rowCount = result.RowCount,
        durationMs = result.DurationMs,
    });
});

app.Run("http://127.0.0.1:5051");

public sealed record ConnectionRequest(Guid Id, string Name, string DatabaseType, string Host, int Port, string Database, string Username, string? Password);
public sealed record QueryRequest(string Sql);
