using System.Text.Json;
using CultPodcasts.DatabasePublisher.Consistency;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Providers;

namespace CultPodcasts.DatabasePublisher;

/// <summary>The output folder cannot be used as it is (missing, unreadable, or wrongly empty or populated).</summary>
public sealed class PublicationFolderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// The folder the public files live in: the previous publish is read from it, and only the files a plan names
/// are changed in it. It serialises exactly as <c>FileRepository.Write</c> does, so file bytes are unchanged,
/// but unlike <c>SafeFileEntityWriter</c> it does not need the folder to be empty: <see cref="WriteMode.Create"/>
/// refuses a file that exists and <see cref="WriteMode.Replace"/> refuses one that does not.
/// </summary>
public sealed class PublicationFolder
{
    private const string FileExtension = ".json";
    private readonly JsonSerializerOptions _options;

    public PublicationFolder(string folderPath, IJsonSerializerOptionsProvider optionsProvider)
    {
        FolderPath = folderPath;
        _options = optionsProvider.GetJsonSerializerOptions();
        _options.WriteIndented = true;
    }

    public string FolderPath { get; }

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

    public async Task WriteAsync(CosmosSelector data, WriteMode mode)
    {
        var path = PathOf(data.FileKey);
        if (mode == WriteMode.Create && File.Exists(path))
        {
            throw new InvalidOperationException(
                $"'{data.FileKey}.json' already exists but was planned as a new file (item {data.Id}); nothing may overwrite it unplanned.");
        }

        if (mode == WriteMode.Replace && !File.Exists(path))
        {
            throw new InvalidOperationException(
                $"'{data.FileKey}.json' was planned as a replacement (item {data.Id}) but does not exist.");
        }

        await using var stream = new FileStream(
            path,
            mode == WriteMode.Create ? FileMode.CreateNew : FileMode.Truncate,
            FileAccess.Write,
            FileShare.None);
        await JsonSerializer.SerializeAsync(stream, data, data.GetType(), _options);
    }

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
