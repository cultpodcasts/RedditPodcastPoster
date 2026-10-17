using System.Text.Json;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>One file whose content now lives in another file: the old and new file names relative to the output folder.</summary>
/// <param name="Id">The id of the item the old file was for.</param>
/// <param name="NewId">The id of the item the new file is for (a film has its episode's id, not the podcast's).</param>
/// <param name="Kind">The kind of the new file: <c>podcast</c>, <c>tvShow</c>, <c>newsOrganisation</c> or <c>film</c>.</param>
/// <param name="Reason"><c>renamed</c> (same kind, new file key) or <c>migrated</c> (a different kind).</param>
public sealed record MigrationEntry(Guid Id, Guid NewId, string OldFile, string NewFile, string Kind, string Reason);

/// <summary>
/// A machine-readable list of the files a publish moved or replaced under another name, for a tool that wants to
/// check what git made of them (for example <c>git diff --cached -M --name-status</c> after <c>git add -A</c>).
/// It is only written where <c>--migrations-out</c> says, which must not be inside the output folder, so it can
/// never be published into the data repository.
/// </summary>
public static class MigrationManifest
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// One entry per old file and successor, for every renamed or migrated file the plan acts on (moved, or
    /// deleted because its target already existed), ordered by old then new file name.
    /// </summary>
    public static IReadOnlyList<MigrationEntry> Create(PublicationPlan plan) =>
        plan.Removals
            .Where(r => r.IsResolved && r.Reason is RemovalReason.Renamed or RemovalReason.Migrated && r.Id is not null)
            .SelectMany(r => r.Successors.Select(s => new MigrationEntry(
                r.Id!.Value,
                s.Id,
                r.FileKey + ".json",
                s.FileKey + ".json",
                JsonNamingPolicy.CamelCase.ConvertName(s.Kind.ToString()),
                r.Reason == RemovalReason.Renamed ? "renamed" : "migrated")))
            .OrderBy(e => e.OldFile, StringComparer.Ordinal)
            .ThenBy(e => e.NewFile, StringComparer.Ordinal)
            .ToList();

    /// <summary>Writes <c>{ "version": 1, "migrations": [...] }</c> through a temporary file so it is never half-written.</summary>
    public static async Task WriteAsync(string path, IReadOnlyList<MigrationEntry> migrations)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, new { version = 1, migrations }, Options);
        }

        File.Move(temporary, full, overwrite: true);
    }
}
