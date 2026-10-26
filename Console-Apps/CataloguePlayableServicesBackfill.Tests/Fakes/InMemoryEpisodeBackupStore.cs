using CataloguePlayableServicesBackfill;
using RedditPodcastPoster.Models.Services;

namespace CataloguePlayableServicesBackfill.Tests.Fakes;

public sealed class InMemoryEpisodeBackupStore : IEpisodeBackupStore
{
    public Dictionary<Guid, Dictionary<string, ServiceLink>> ById { get; } = new();

    public HashSet<Guid> Unreadable { get; } = [];

    public string? ResolvePath(string backupDir, Guid id)
    {
        if (!ById.ContainsKey(id) || Unreadable.Contains(id))
        {
            return null;
        }

        return Path.Combine(backupDir, "episode", $"{id}.json");
    }

    public bool TryLoadServices(string path, out Dictionary<string, ServiceLink> services)
    {
        services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        var name = Path.GetFileNameWithoutExtension(path);
        if (!Guid.TryParse(name, out var id) || !ById.TryGetValue(id, out var map))
        {
            return false;
        }

        services = PlayableServicesDiscrepancyCarry.CloneServices(map)
                   ?? new Dictionary<string, ServiceLink>(StringComparer.Ordinal);
        return true;
    }
}
