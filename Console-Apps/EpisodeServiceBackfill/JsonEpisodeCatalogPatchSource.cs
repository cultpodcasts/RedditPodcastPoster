using RedditPodcastPoster.Models.Episodes;

namespace EpisodeServiceBackfill;

/// <summary>
/// Library path: leftover merge from <see cref="JsonElement"/> because typed
/// <see cref="Episode"/> no longer has leftover members.
/// </summary>
public sealed class JsonEpisodeCatalogPatchSource : IEpisodeCatalogPatchSource
{
    public bool TryCreate(string json, out EpisodeServiceCatalogPatch? patch) =>
        EpisodeServiceCatalogPatchFactory.TryCreate(json, out patch);
}
