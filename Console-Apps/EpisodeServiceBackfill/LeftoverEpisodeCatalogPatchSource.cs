using RedditPodcastPoster.Models.Episodes;

namespace EpisodeServiceBackfill;

/// <summary>
/// CLI backfill path: leftover members live on <see cref="LeftoverEpisodeDocument"/>
/// (subclass of <see cref="Episode"/>), not on the production DTO.
/// </summary>
public sealed class LeftoverEpisodeCatalogPatchSource : IEpisodeCatalogPatchSource
{
    public bool TryCreate(string json, out EpisodeServiceCatalogPatch? patch)
    {
        patch = null;
        if (!LeftoverEpisodeDocument.TryParse(json, out var leftover) || leftover is null)
        {
            return false;
        }

        return leftover.TryCreateCatalogPatch(out patch);
    }
}
