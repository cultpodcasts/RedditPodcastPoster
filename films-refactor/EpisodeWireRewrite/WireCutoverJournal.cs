using System.Text.Json;
using System.Text.Json.Serialization;

namespace EpisodeWireRewrite;

/// <summary>
/// Append-only journal of applied (or dry-run planned) changes with full Before/After
/// snapshots plus an explicit from→to change log for operator repair if rollback fails.
/// </summary>
public sealed class WireCutoverJournal : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly StreamWriter _writer;
    private readonly StreamWriter _idsWriter;
    private readonly StreamWriter _appliedIdsWriter;
    private readonly StreamWriter _changesWriter;
    private readonly object _gate = new();

    public const int SchemaVersion = 1;

    public WireCutoverJournal(string journalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        Path = System.IO.Path.GetFullPath(journalPath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var stem = System.IO.Path.ChangeExtension(Path, null);
        AffectedIdsPath = stem + "-affected-ids.txt";
        AppliedIdsPath = stem + "-applied-ids.txt";
        ChangesPath = stem + "-changes.txt";
        _writer = new StreamWriter(new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.Read));
        _idsWriter = new StreamWriter(new FileStream(AffectedIdsPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        _appliedIdsWriter = new StreamWriter(new FileStream(AppliedIdsPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        _changesWriter = new StreamWriter(new FileStream(ChangesPath, FileMode.Create, FileAccess.Write, FileShare.Read));
        _changesWriter.WriteLine("# from -> to per cutover field. Repair = restore 'before' from journal JSONL for applied=true rows.");
        _changesWriter.WriteLine("# See films-refactor/REPAIR.md");
        _changesWriter.Flush();
    }

    public string Path { get; }
    public string AffectedIdsPath { get; }
    public string AppliedIdsPath { get; }
    public string ChangesPath { get; }

    public void Append(WireCutoverChange change, bool applied)
    {
        ArgumentNullException.ThrowIfNull(change);
        var entry = new WireCutoverJournalEntry
        {
            SchemaVersion = SchemaVersion,
            EpisodeId = change.EpisodeId,
            PodcastId = change.PodcastId,
            Applied = applied,
            Reason = change.Reason,
            Before = change.Before,
            After = change.After,
            FieldChanges = change.FieldChanges,
            RecordedUtc = DateTime.UtcNow
        };
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (_gate)
        {
            _writer.WriteLine(line);
            _writer.Flush();
            _idsWriter.WriteLine($"{change.EpisodeId}\t{change.PodcastId}");
            _idsWriter.Flush();
            if (applied)
            {
                _appliedIdsWriter.WriteLine($"{change.EpisodeId}\t{change.PodcastId}");
                _appliedIdsWriter.Flush();
            }

            _changesWriter.WriteLine(
                $"episodeId={change.EpisodeId} podcastId={change.PodcastId} applied={applied} utc={entry.RecordedUtc:O}");
            foreach (var fieldChange in change.FieldChanges)
            {
                _changesWriter.WriteLine("  " + fieldChange.ToDisplayLine());
            }

            _changesWriter.WriteLine();
            _changesWriter.Flush();
        }
    }

    public static IReadOnlyList<WireCutoverJournalEntry> ReadAll(string journalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(journalPath);
        if (!File.Exists(journalPath))
        {
            throw new FileNotFoundException("Wire cutover journal not found.", journalPath);
        }

        var list = new List<WireCutoverJournalEntry>();
        foreach (var line in File.ReadLines(journalPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<WireCutoverJournalEntry>(line, JsonOptions)
                        ?? throw new InvalidOperationException("Journal line deserialized to null.");
            list.Add(entry);
        }

        return list;
    }

    public void Dispose()
    {
        _writer.Dispose();
        _idsWriter.Dispose();
        _appliedIdsWriter.Dispose();
        _changesWriter.Dispose();
    }
}

public sealed class WireCutoverJournalEntry
{
    public int SchemaVersion { get; init; } = WireCutoverJournal.SchemaVersion;
    public Guid EpisodeId { get; init; }
    public Guid PodcastId { get; init; }
    public bool Applied { get; init; }
    public string Reason { get; init; } = "";
    public required IReadOnlyList<WireFieldState> Before { get; init; }
    public required IReadOnlyList<WireFieldState> After { get; init; }
    public IReadOnlyList<WireFieldChange> FieldChanges { get; init; } = [];
    public DateTime RecordedUtc { get; init; }

    public WireCutoverChange ToChange() => new()
    {
        EpisodeId = EpisodeId,
        PodcastId = PodcastId,
        Before = Before,
        After = After,
        FieldChanges = FieldChanges.Count > 0
            ? FieldChanges
            : WireCutoverPlanner.Diff(Before, After),
        Reason = Reason
    };
}
