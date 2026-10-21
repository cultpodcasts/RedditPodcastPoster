using System.Text.Json;
using CultPodcasts.DatabasePublisher.Consistency;
using CultPodcasts.DatabasePublisher.Mapping;
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

    /// <summary>
    /// Whether <paramref name="path"/> is the folder itself or anywhere beneath it. Every existing part of both
    /// paths is resolved first (symbolic links and junctions followed, short names and case as the file system
    /// has them), so a path reached through a link into the folder is contained. Limits: a link created after
    /// the check, and hard links to a file, are not seen.
    /// </summary>
    public bool Contains(string path)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var root = Resolve(FolderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Resolve(path);
        return full.Equals(root, comparison) ||
               full.StartsWith(root + Path.DirectorySeparatorChar, comparison) ||
               full.StartsWith(root + Path.AltDirectorySeparatorChar, comparison);
    }

    private static string Resolve(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        var parts = full[root.Length..].Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var candidate = Path.Combine(current, parts[i]);
            var entry = Directory.Exists(candidate) ? new DirectoryInfo(candidate)
                : File.Exists(candidate) ? (FileSystemInfo)new FileInfo(candidate)
                : null;
            if (entry is null)
            {
                // nothing exists from here on: the rest cannot be a link
                return Path.Combine([current, .. parts[i..]]);
            }

            current = entry.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? RealName(current, entry);
        }

        return current;
    }

    /// <summary>The name the file system itself holds for an entry (long name and true case, not a short name).</summary>
    private static string RealName(string parent, FileSystemInfo entry)
    {
        try
        {
            var real = Directory.EnumerateFileSystemEntries(parent, entry.Name).FirstOrDefault();
            return real is null ? entry.FullName : Path.Combine(parent, Path.GetFileName(real));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return entry.FullName;
        }
    }
    /// <summary>
    /// Reads the top-level <c>*.json</c> files, the same files update.ps1 used to clear, taking the id from
    /// each, for unprefixed (podcast) files the episode ids, and for every kind the release text of each episode
    /// or report. Each file is read once; a file that exists but cannot be read (locked, no access) fails the
    /// whole read with <see cref="PublicationFolderException"/>, so nothing is planned or changed from a partial
    /// picture, whereas a file that is not JSON of the expected shape is a file with no id.
    /// </summary>
    public async Task<IReadOnlyList<BaselineFile>> ReadBaselineAsync()
    {
        EnsureExists();

        try
        {
            var files = new List<BaselineFile>();
            foreach (var path in Directory.EnumerateFiles(FolderPath, "*" + FileExtension, SearchOption.TopDirectoryOnly)
                         .Where(p => Path.GetExtension(p).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
                         .Order(StringComparer.Ordinal))
            {
                var fileKey = Path.GetFileNameWithoutExtension(path);
                var info = new FileInfo(path);
                files.Add((await ReadAsync(path, fileKey)) with
                {
                    Length = info.Length,
                    LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks
                });
            }

            return files;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PublicationFolderException(
                $"The output folder '{FolderPath}' could not be read: {ex.Message}", ex);
        }
    }

    /// <summary>Fails with <see cref="PublicationFolderException"/> when the folder does not exist.</summary>
    public void EnsureExists()
    {
        if (!Directory.Exists(FolderPath))
        {
            throw new PublicationFolderException($"The output folder '{FolderPath}' does not exist.");
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

    /// <summary>
    /// Fails when the folder's <c>*.json</c> files are no longer the ones the plan was made from: a file added or
    /// removed, or one whose length or last-write time differs from when it was read. It does not hash content.
    /// </summary>
    public void EnsureUnchanged(IReadOnlyCollection<BaselineFile> baseline)
    {
        var now = Directory.EnumerateFiles(FolderPath, "*" + FileExtension, SearchOption.TopDirectoryOnly)
            .Where(p => Path.GetExtension(p).Equals(FileExtension, StringComparison.OrdinalIgnoreCase))
            .Select(p => new FileInfo(p))
            .ToDictionary(i => Path.GetFileNameWithoutExtension(i.Name), StringComparer.Ordinal);
        var same = now.Count == baseline.Count && baseline.All(b =>
            now.TryGetValue(b.FileKey, out var info) &&
            info.Length == b.Length &&
            info.LastWriteTimeUtc.Ticks == b.LastWriteUtcTicks);
        if (!same)
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

            return new BaselineFile(
                fileKey,
                guid,
                PublicKindExtensions.FromFileKey(fileKey) == PublicKind.Podcast ? ChildIds(root) : [],
                Releases: BaselineReleaseText.FromDocument(root));
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
