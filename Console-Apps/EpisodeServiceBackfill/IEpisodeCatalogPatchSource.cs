namespace EpisodeServiceBackfill;

/// <summary>
/// Builds a surgical <c>services</c>/<c>ids</c> patch from a raw Cosmos episode JSON string.
/// </summary>
public interface IEpisodeCatalogPatchSource
{
    bool TryCreate(string json, out EpisodeServiceCatalogPatch? patch);
}
