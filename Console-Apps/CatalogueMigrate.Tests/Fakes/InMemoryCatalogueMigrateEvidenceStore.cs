using System.Text.Json;
using CatalogueMigrate;
using RedditPodcastPoster.Persistence.Providers;

namespace CatalogueMigrate.Tests.Fakes;

/// <summary>
/// Evidence batches kept in memory. Records go through the Cosmos JSON options,
/// so a rollback reads the same snapshot shape the file store writes.
/// </summary>
public sealed class InMemoryCatalogueMigrateEvidenceStore : ICatalogueMigrateEvidenceStore
{
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    public Dictionary<string, List<string>> Files { get; } = new();

    public ICatalogueMigrateEvidenceBatch OpenBatch(string directory, string runId, string kind, int batchNumber)
    {
        var name = $"{directory}/{runId}-{kind}-batch-{batchNumber:D3}.jsonl";
        if (!Files.TryGetValue(name, out var lines))
        {
            lines = [];
            Files[name] = lines;
        }

        return new Batch(name, lines, _options);
    }

    public bool Exists(string path) => Files.ContainsKey(path);

    public async IAsyncEnumerable<CatalogueMigrateEvidenceRecord> Read(string path)
    {
        await Task.CompletedTask;
        foreach (var line in Files[path])
        {
            yield return JsonSerializer.Deserialize<CatalogueMigrateEvidenceRecord>(line, _options)!;
        }
    }

    public IReadOnlyList<CatalogueMigrateEvidenceRecord> Records(string path) =>
        Files[path].Select(line => JsonSerializer.Deserialize<CatalogueMigrateEvidenceRecord>(line, _options)!).ToList();

    private sealed class Batch(string name, List<string> lines, JsonSerializerOptions options)
        : ICatalogueMigrateEvidenceBatch
    {
        public string Name => name;

        public Task Write(CatalogueMigrateEvidenceRecord record)
        {
            lines.Add(JsonSerializer.Serialize(record, options));
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
