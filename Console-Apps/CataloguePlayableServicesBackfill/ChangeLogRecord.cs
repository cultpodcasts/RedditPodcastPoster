using System.Text.Json.Serialization;

namespace CataloguePlayableServicesBackfill;

public sealed class ChangeLogRecord
{
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    [JsonPropertyName("id")]
    public required Guid Id { get; init; }

    [JsonPropertyName("parentId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ParentId { get; init; }

    [JsonPropertyName("backupPath")]
    public required string BackupPath { get; init; }

    [JsonPropertyName("publicDatabasePath")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string PublicDatabasePath { get; init; } = string.Empty;

    [JsonPropertyName("changes")]
    public required IReadOnlyList<ServiceFieldChange> Changes { get; init; }

    [JsonPropertyName("applied")]
    public required bool Applied { get; init; }

    [JsonPropertyName("utc")]
    public required DateTimeOffset Utc { get; init; }

    [JsonPropertyName("missingBackup")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool MissingBackup { get; init; }
}

public interface IChangeLogWriter : IAsyncDisposable
{
    Task WriteAsync(ChangeLogRecord record, CancellationToken cancellationToken = default);
}

public sealed class JsonlChangeLogWriter : IChangeLogWriter
{
    private readonly StreamWriter _writer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonlChangeLogWriter(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _writer = new StreamWriter(new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
    }

    public async Task WriteAsync(ChangeLogRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var line = System.Text.Json.JsonSerializer.Serialize(record, BackupDocumentJsonOptions.Instance);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        _gate.Dispose();
    }
}
