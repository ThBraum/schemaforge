using SchemaForge.Application;
using SchemaForge.Infrastructure;
using SchemaForge.Domain;
using System.Net;
using System.Text;
using System.Text.Json;

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
builder.Services.AddSingleton<ISavedQueryStore>(_ => new SqliteSavedQueryStore(appDbPath));
builder.Services.AddSingleton<IQueryHistoryStore>(_ => new SqliteQueryHistoryStore(appDbPath));
builder.Services.AddSingleton<ISchemaSnapshotStore>(_ => new SqliteSchemaSnapshotStore(appDbPath));
builder.Services.AddSingleton<IDatabaseProvider, PostgresDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProvider, MySqlDatabaseProvider>();
builder.Services.AddSingleton<IDatabaseProviderResolver, DatabaseProviderResolver>();
builder.Services.AddScoped<ExplorerService>();
builder.Services.AddScoped<SavedQueryService>();
builder.Services.AddScoped<QueryHistoryService>();
builder.Services.AddScoped<SchemaSnapshotService>();
builder.Services.AddSingleton<ISchemaDiffEngine, SchemaDiffEngine>();
builder.Services.AddScoped<SchemaDiffService>();

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

app.MapGet("/api/query/history/{connectionId:guid}", async (Guid connectionId, int? limit, QueryHistoryService service, CancellationToken ct) =>
{
    var result = await service.ListByConnectionAsync(connectionId, limit ?? 100, ct);
    return Results.Ok(result.Select(entry => new
    {
        id = entry.Id,
        connectionId = entry.ConnectionId,
        sql = entry.Sql,
        status = entry.Status == QueryExecutionStatus.Succeeded ? "succeeded" : "failed",
        durationMs = entry.DurationMs,
        errorMessage = entry.ErrorMessage,
        executedAtUtc = entry.ExecutedAtUtc,
    }));
});

app.MapGet("/api/saved-queries/{connectionId:guid}", async (Guid connectionId, SavedQueryService service, CancellationToken ct) =>
{
    var result = await service.ListByConnectionAsync(connectionId, ct);
    return Results.Ok(result.Select(item => new
    {
        id = item.Id,
        connectionId = item.ConnectionId,
        title = item.Title,
        sql = item.Sql,
        tags = item.Tags,
        createdAtUtc = item.CreatedAtUtc,
        updatedAtUtc = item.UpdatedAtUtc,
    }));
});

app.MapPost("/api/saved-queries", async (SaveSavedQueryRequest request, SavedQueryService service, CancellationToken ct) =>
{
    var saved = await service.UpsertAsync(request.Id, request.ConnectionId, request.Title, request.Sql, request.Tags, ct);
    return Results.Ok(new
    {
        id = saved.Id,
        connectionId = saved.ConnectionId,
        title = saved.Title,
        sql = saved.Sql,
        tags = saved.Tags,
        createdAtUtc = saved.CreatedAtUtc,
        updatedAtUtc = saved.UpdatedAtUtc,
    });
});

app.MapPut("/api/saved-queries/{id:guid}", async (Guid id, SaveSavedQueryRequest request, SavedQueryService service, CancellationToken ct) =>
{
    var saved = await service.UpsertAsync(id, request.ConnectionId, request.Title, request.Sql, request.Tags, ct);
    return Results.Ok(new
    {
        id = saved.Id,
        connectionId = saved.ConnectionId,
        title = saved.Title,
        sql = saved.Sql,
        tags = saved.Tags,
        createdAtUtc = saved.CreatedAtUtc,
        updatedAtUtc = saved.UpdatedAtUtc,
    });
});

app.MapDelete("/api/saved-queries/{id:guid}", async (Guid id, SavedQueryService service, CancellationToken ct) =>
{
    await service.DeleteAsync(id, ct);
    return Results.NoContent();
});

app.MapPost("/api/snapshots/capture/{connectionId:guid}", async (Guid connectionId, CaptureSnapshotRequest request, SchemaSnapshotService service, CancellationToken ct) =>
{
    var snapshot = await service.CaptureAsync(connectionId, request.Name, ct);
    return Results.Ok(new
    {
        id = snapshot.Id,
        connectionId = snapshot.ConnectionId,
        name = snapshot.Name,
        createdAtUtc = snapshot.CreatedAtUtc,
        structure = snapshot.Structure,
    });
});

app.MapGet("/api/snapshots/{connectionId:guid}", async (Guid connectionId, SchemaSnapshotService service, CancellationToken ct) =>
{
    var result = await service.ListByConnectionAsync(connectionId, ct);
    return Results.Ok(result.Select(item => new
    {
        id = item.Id,
        connectionId = item.ConnectionId,
        name = item.Name,
        createdAtUtc = item.CreatedAtUtc,
        schemaCount = item.Structure.Schemas.Count,
        tableCount = item.Structure.Schemas.Sum(schema => schema.Tables.Count),
    }));
});

app.MapGet("/api/snapshots/item/{snapshotId:guid}", async (Guid snapshotId, SchemaSnapshotService service, CancellationToken ct) =>
{
    var snapshot = await service.GetByIdOrThrowAsync(snapshotId, ct);
    return Results.Ok(new
    {
        id = snapshot.Id,
        connectionId = snapshot.ConnectionId,
        name = snapshot.Name,
        createdAtUtc = snapshot.CreatedAtUtc,
        structure = snapshot.Structure,
    });
});

app.MapPost("/api/schema-diff/compare", async (CompareSnapshotsRequest request, SchemaDiffService service, CancellationToken ct) =>
{
        var diff = await service.CompareAsync(request.SourceSnapshotId, request.TargetSnapshotId, ct);
        return Results.Ok(diff);
});

app.MapGet("/api/schema-diff/details", async (Guid sourceSnapshotId, Guid targetSnapshotId, SchemaDiffService service, CancellationToken ct) =>
{
        var diff = await service.CompareAsync(sourceSnapshotId, targetSnapshotId, ct);
        return Results.Ok(diff);
});

app.MapGet("/api/schema-diff/export/json", async (Guid sourceSnapshotId, Guid targetSnapshotId, SchemaDiffService service, CancellationToken ct) =>
{
        var diff = await service.CompareAsync(sourceSnapshotId, targetSnapshotId, ct);
        var json = JsonSerializer.Serialize(diff, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
                WriteIndented = true,
        });

        var fileName = $"schema-diff-{diff.SourceSnapshotId:N}-{diff.TargetSnapshotId:N}.json";
        return Results.File(Encoding.UTF8.GetBytes(json), "application/json", fileName);
});

app.MapGet("/api/schema-diff/export/html", async (Guid sourceSnapshotId, Guid targetSnapshotId, SchemaDiffService service, CancellationToken ct) =>
{
        var diff = await service.CompareAsync(sourceSnapshotId, targetSnapshotId, ct);
        var htmlBuilder = new StringBuilder();
        htmlBuilder.AppendLine("<!doctype html>");
        htmlBuilder.AppendLine("<html lang=\"en\">");
        htmlBuilder.AppendLine("<head>");
        htmlBuilder.AppendLine("<meta charset=\"utf-8\" />");
        htmlBuilder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
        htmlBuilder.AppendLine("<title>Schema Diff Report</title>");
        htmlBuilder.AppendLine("<style>");
        htmlBuilder.AppendLine("body { font-family: Inter, Arial, sans-serif; margin: 32px; color: #0f172a; }");
        htmlBuilder.AppendLine("h1, h2 { margin: 0 0 12px; }");
        htmlBuilder.AppendLine(".small { color: #475569; margin-bottom: 24px; }");
        htmlBuilder.AppendLine(".grid { display: grid; grid-template-columns: repeat(3, minmax(180px, 1fr)); gap: 12px; margin-bottom: 24px; }");
        htmlBuilder.AppendLine(".card { border: 1px solid #cbd5e1; border-radius: 10px; padding: 12px; background: #f8fafc; }");
        htmlBuilder.AppendLine("ul { margin-top: 6px; }");
        htmlBuilder.AppendLine("</style>");
        htmlBuilder.AppendLine("</head>");
        htmlBuilder.AppendLine("<body>");
        htmlBuilder.AppendLine("<h1>Schema Diff Report</h1>");
        htmlBuilder.AppendLine($"<p class=\"small\">Source: {WebUtility.HtmlEncode(diff.SourceSnapshotName)} · Target: {WebUtility.HtmlEncode(diff.TargetSnapshotName)} · Generated: {diff.GeneratedAtUtc:O}</p>");
        htmlBuilder.AppendLine("<div class=\"grid\">");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Tables Added</strong><div>{diff.Summary.TablesAdded}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Tables Removed</strong><div>{diff.Summary.TablesRemoved}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Tables Modified</strong><div>{diff.Summary.TablesModified}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Columns Added</strong><div>{diff.Summary.ColumnsAdded}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Columns Removed</strong><div>{diff.Summary.ColumnsRemoved}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Columns Modified</strong><div>{diff.Summary.ColumnsModified}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Foreign Keys Added</strong><div>{diff.Summary.ForeignKeysAdded}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Foreign Keys Removed</strong><div>{diff.Summary.ForeignKeysRemoved}</div></div>");
        htmlBuilder.AppendLine($"<div class=\"card\"><strong>Breaking Changes</strong><div>{diff.Summary.BreakingChanges}</div></div>");
        htmlBuilder.AppendLine("</div>");
        htmlBuilder.AppendLine("<h2>Breaking Changes</h2>");
        htmlBuilder.AppendLine("<ul>");

        foreach (var item in diff.BreakingChanges)
        {
                var columnSuffix = string.IsNullOrWhiteSpace(item.ColumnName)
                    ? string.Empty
                    : $".{WebUtility.HtmlEncode(item.ColumnName)}";

                htmlBuilder.AppendLine($"<li><strong>{WebUtility.HtmlEncode(item.SchemaName)}.{WebUtility.HtmlEncode(item.TableName)}</strong>{columnSuffix}: {WebUtility.HtmlEncode(item.Description)}</li>");
        }

        htmlBuilder.AppendLine("</ul>");
        htmlBuilder.AppendLine("</body>");
        htmlBuilder.AppendLine("</html>");
        var html = htmlBuilder.ToString();

        var fileName = $"schema-diff-{diff.SourceSnapshotId:N}-{diff.TargetSnapshotId:N}.html";
        return Results.File(Encoding.UTF8.GetBytes(html), "text/html", fileName);
});

app.Run("http://127.0.0.1:5051");

public sealed record ConnectionRequest(Guid Id, string Name, string DatabaseType, string Host, int Port, string Database, string Username, string? Password);
public sealed record QueryRequest(string Sql);
public sealed record SaveSavedQueryRequest(Guid? Id, Guid ConnectionId, string Title, string Sql, string[]? Tags);
public sealed record CaptureSnapshotRequest(string? Name);
public sealed record CompareSnapshotsRequest(Guid SourceSnapshotId, Guid TargetSnapshotId);
