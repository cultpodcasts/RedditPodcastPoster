using System.Text.Json;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Providers;

namespace CatalogueMigrate;

public static class CatalogueMigrateEvidenceEvent
{
    /// <summary>Written before any write. Holds the full source podcast and every episode.</summary>
    public const string Begin = "begin";
    public const string Conflict = "conflict";
    public const string DestWritten = "destWritten";
    public const string Verified = "verified";
    public const string SearchSwapped = "searchSwapped";
    public const string SourceDeleted = "sourceDeleted";
    public const string Completed = "completed";
    public const string Failed = "failed";

    public const string RollbackRestored = "rollbackSourceRestored";
    public const string RollbackSearchRestored = "rollbackSearchRestored";
    public const string RollbackCompleted = "rollbackCompleted";
    public const string RollbackRefused = "rollbackRefused";
}

public sealed record CatalogueMigrateEvidenceRecord(
    string Event,
    string RunId,
    string Kind,
    Guid PodcastId,
    Guid? DestParentId,
    Guid[] DestPlayableIds,
    DateTimeOffset At,
    Podcast? BeforePodcast = null,
    Episode[]? BeforeEpisodes = null,
    string? Detail = null);

/// <summary>One evidence file per batch. Records are flushed as they are written.</summary>
public interface ICatalogueMigrateEvidenceBatch : IAsyncDisposable
{
    string Name { get; }
    Task Write(CatalogueMigrateEvidenceRecord record);
}

public interface ICatalogueMigrateEvidenceStore
{
    ICatalogueMigrateEvidenceBatch OpenBatch(string directory, string runId, string kind, int batchNumber);
    bool Exists(string path);
    IAsyncEnumerable<CatalogueMigrateEvidenceRecord> Read(string path);
}

/// <summary>JSON lines, one record per line, using the Cosmos serializer options so snapshots round-trip.</summary>
public sealed class FileCatalogueMigrateEvidenceStore(IJsonSerializerOptionsProvider optionsProvider)
    : ICatalogueMigrateEvidenceStore
{
    private readonly JsonSerializerOptions _options = optionsProvider.GetJsonSerializerOptions();

    public ICatalogueMigrateEvidenceBatch OpenBatch(string directory, string runId, string kind, int batchNumber)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{runId}-{kind}-batch-{batchNumber:D3}.jsonl");
        return new FileBatch(path, _options);
    }

    public bool Exists(string path) => File.Exists(path);

    public async IAsyncEnumerable<CatalogueMigrateEvidenceRecord> Read(string path)
    {
        using var reader = new StreamReader(path);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            yield return JsonSerializer.Deserialize<CatalogueMigrateEvidenceRecord>(line, _options)
                         ?? throw new InvalidDataException($"Empty evidence line in '{path}'.");
        }
    }

    private sealed class FileBatch(string path, JsonSerializerOptions options) : ICatalogueMigrateEvidenceBatch
    {
        private readonly StreamWriter _writer = new(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read));

        public string Name => path;

        public async Task Write(CatalogueMigrateEvidenceRecord record)
        {
            await _writer.WriteLineAsync(JsonSerializer.Serialize(record, options));
            await _writer.FlushAsync();
        }

        public ValueTask DisposeAsync() => _writer.DisposeAsync();
    }
}
