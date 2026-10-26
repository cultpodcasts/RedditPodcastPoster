using RedditPodcastPoster.Models.Services;

namespace CataloguePlayableServicesBackfill;

public interface IEpisodeBackupStore
{
    /// <summary>Resolves the episode backup path for <paramref name="id"/>, or null when the file is missing.</summary>
    string? ResolvePath(string backupDir, Guid id);

    /// <summary>Loads and builds the backup services map. Returns false when the file is missing or unreadable.</summary>
    bool TryLoadServices(string path, out Dictionary<string, ServiceLink> services);
}

public sealed class FileEpisodeBackupStore : IEpisodeBackupStore
{
    public string? ResolvePath(string backupDir, Guid id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupDir);
        var root = Path.GetFullPath(backupDir);
        var asEpisodeChild = Path.Combine(root, "episode", $"{id}.json");
        if (File.Exists(asEpisodeChild))
        {
            return asEpisodeChild;
        }

        var asDirect = Path.Combine(root, $"{id}.json");
        return File.Exists(asDirect) ? asDirect : null;
    }

    public bool TryLoadServices(string path, out Dictionary<string, ServiceLink> services)
    {
        services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (IOException)
        {
            return false;
        }

        if (!EpisodeBackupDocument.TryParse(json, out var document) || document is null)
        {
            return false;
        }

        services = EpisodeBackupServicesBuilder.Build(document);
        return true;
    }
}
