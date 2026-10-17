using System.Text.Json;
using CultPodcasts.DatabasePublisher.Consistency;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Providers;

namespace CultPodcasts.DatabasePublisher;

/// <summary>The output folder cannot be used as it is (missing, unreadable, or wrongly empty or populated).</summary>
public sealed class PublicationFolderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// Applying a plan failed after the first file was touched, so the folder may be partly updated and the
/// staged <c>*.json.tmp</c> files that were not yet committed are still there.
/// </summary>
public sealed class PublicationApplyException(string message, Exception innerException)
    : Exception(message, innerException);

/// <summary>
/// The folder the public files live in: the previous publish is read from it, and only the files a plan names
/// are changed in it. It serialises exactly as <c>FileRepository.Write</c> does, so file bytes are unchanged,
/// but unlike <c>SafeFileEntityWriter</c> it does not need the folder to be empty. New content is first
/// staged next to its target as <c>{key}.json.tmp</c> (same volume, so the final swap is a rename and a
/// file is never half-written); <see cref="Commit"/> then swaps it in, where <see cref="WriteMode.Create"/>
/// refuses a file that exists and <see cref="WriteMode.Replace"/> refuses one that does not.
/// </summary>
public class PublicationFolder
{
    private const string FileExtension = ".json";
    private const string StagingExtension = ".tmp";
    private readonly JsonSerializerOptions _options;

    public PublicationFolder(string folderPath, IJsonSerializerOptionsProvider optionsProvider)
    {
        FolderPath = folderPath;
        _options = optionsProvider.GetJsonSerializerOptions();
        _options.WriteIndented = true;
    }

    public string FolderPath { get; }

    /// <summary>Whether <paramref name="path"/> is the folder itself or anywhere beneath it.</summary>
    public bool Contains(string path)
    {
        var root = Path.GetFullPath(FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(path);
        return full.Equals(root, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               full.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads the top-level <c>*.json</c> files, the same files update.ps1 used to clear, taking the id from
    /// each and, for unprefixed (podcast) files, the episode ids.
    /// </summary>
    public async Task<IReadOnlyList<BaselineFile>> ReadBaselineAsync()
    {
        if (!Directory.Exists(FolderPath))
        {
            throw new PublicationFolderException($"The output folder '{FolderPath}' does not exist.");
        }

        try
        {
            var files = new List<BaselineFile>();
            foreach (var path in Directory.EnumerateFiles(FolderPath, "*" + FileExtension, SearchOption.TopDirectoryOnly)
                         .Where(p => Path.GetExtension(p).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
                         .Order(StringComparer.Ordinal))
            {
                var fileKey = Path.GetFileNameWithoutExtension(path);
                files.Add(await ReadAsync(path, fileKey));
            }

            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PublicationFolderException(
                $"The output folder '{FolderPath}' could not be read: {ex.Message}", ex);
        }
    }

    public void Delete(string fileKey) => File.Delete(PathOf(fileKey));

    /// <summary>Renames a file in place; the destination must not exist.</summary>
    public void Move(string fromFileKey, string toFileKey) =>
        File.Move(PathOf(fromFileKey), PathOf(toFileKey), overwrite: false);

    /// <summary>The staged files (<c>*.json.tmp</c>) an interrupted run left behind, by file name.</summary>
    public IReadOnlyList<string> FindStaging() =>
        Directory.EnumerateFiles(FolderPath, "*" + FileExtension + StagingExtension, SearchOption.TopDirectoryOnly)
            .Select(p => Path.GetFileName(p)!)
            .Where(n => n.EndsWith(FileExtension + StagingExtension, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>Deletes every staged file; returns how many there were.</summary>
    public int DiscardStaging()
    {
        var staged = FindStaging();
        foreach (var name in staged)
        {
            File.Delete(Path.Combine(FolderPath, name));
        }

        return staged.Count;
    }

    /// <summary>Fails when the folder's <c>*.json</c> files are no longer the ones the plan was made from.</summary>
    public void EnsureUnchanged(IReadOnlyCollection<BaselineFile> baseline)
    {
        var now = Directory.EnumerateFiles(FolderPath, "*" + FileExtension, SearchOption.TopDirectoryOnly)
            .Where(p => Path.GetExtension(p).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
            .Select(p => Path.GetFileNameWithoutExtension(p)!)
            .ToHashSet(StringComparer.Ordinal);
        if (!now.SetEquals(baseline.Select(b => b.FileKey)))
        {
            throw new PublicationFolderException(
                $"The files in '{FolderPath}' changed while the publish was being prepared; nothing was changed. Run again.");
        }
    }

    /// <summary>Writes the new content of a file to <c>{key}.json.tmp</c>; the real file is not touched.</summary>
    public async Task StageAsync(CosmosSelector data)
    {
        await using var stream = new FileStream(
            StagingPathOf(data.FileKey), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, data.GetType(), _options);
    }

    /// <summary>Swaps a staged file in over its target (a rename, so never half-written), as planned.</summary>
    public virtual void Commit(string fileKey, WriteMode mode)
    {
        var path = PathOf(fileKey);
        if (mode == WriteMode.Create && File.Exists(path))
        {
            throw new InvalidOperationException(
                $"'{fileKey}.json' already exists but was planned as a new file; nothing may overwrite it unplanned.");
        }

        if (mode == WriteMode.Replace && !File.Exists(path))
        {
            throw new InvalidOperationException(
                $"'{fileKey}.json' was planned as a replacement but does not exist.");
        }

        File.Move(StagingPathOf(fileKey), path, overwrite: true);
    }

    private string StagingPathOf(string fileKey) => PathOf(fileKey) + StagingExtension;
    private string PathOf(string fileKey)
    {
        if (string.IsNullOrWhiteSpace(fileKey) || Path.GetFileName(fileKey) != fileKey)
        {
            throw new InvalidOperationException($"'{fileKey}' is not a valid file-key for a file in the output folder.");
        }

        return Path.Combine(FolderPath, fileKey + FileExtension);
    }

    private static async Task<BaselineFile> ReadAsync(string path, string fileKey)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String ||
                !id.TryGetGuid(out var guid))
            {
                return new BaselineFile(fileKey, null, []);
            }

            return new BaselineFile(fileKey, guid, PublicKindExtensions.FromFileKey(fileKey) == PublicKind.Podcast
                ? ChildIds(root)
                : []);
        }
        catch (JsonException)
        {
            return new BaselineFile(fileKey, null, []);
        }
    }

    private static List<Guid> ChildIds(JsonElement root)
    {
        var ids = new List<Guid>();
        if (root.TryGetProperty("episodes", out var episodes) && episodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var episode in episodes.EnumerateArray())
            {
                if (episode.ValueKind == JsonValueKind.Object &&
                    episode.TryGetProperty("id", out var id) &&
                    id.ValueKind == JsonValueKind.String &&
                    id.TryGetGuid(out var guid))
                {
                    ids.Add(guid);
                }
            }
        }

        return ids;
    }
}
