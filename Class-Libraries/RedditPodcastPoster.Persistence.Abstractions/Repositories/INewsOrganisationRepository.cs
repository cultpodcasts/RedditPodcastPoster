using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

public interface INewsOrganisationRepository : IRepository<NewsOrganisation>, IFilterableRepository<NewsOrganisation>
{
    Task<NewsOrganisation?> GetNewsOrganisation(Guid newsOrganisationId);
    Task Delete(Guid newsOrganisationId);

    /// <summary>
    /// Patch only the Indexer-owned fields (<see cref="CatalogueParentIndexerPatch"/>), so a curator edit,
    /// rename or removal saved during an index pass is not overwritten. Returns false when the NewsOrganisation no
    /// longer exists.
    /// </summary>
    Task<bool> PatchIndexerState(Guid newsOrganisationId, CatalogueParentIndexerPatch patch);

    /// <summary>
    /// The indexer's write-back of the per-channel flags on <see cref="NewsOrganisation.AdditionalYouTubeChannels"/>
    /// (#1370): sets <c>additionalYouTubeChannels</c> to <paramref name="channels"/> only while the stored list still has
    /// the same channel and playlist ids in the same order, so a curator edit of the channels saved during the pass
    /// wins (the flags are measured again next pass). Returns false when the NewsOrganisation no longer exists.
    /// </summary>
    Task<bool> PatchAdditionalYouTubeChannelFlags(Guid newsOrganisationId, IReadOnlyList<AdditionalYouTubeChannel> channels);

    /// <summary>
    /// Sets only <c>indexing.bypassShortEpisodeChecking</c> and <c>indexing.alwaysPromoteAsHero</c> to true (the
    /// saved news-organisation defaults) with a Cosmos partial patch, so nothing else on the document is written.
    /// <paramref name="indexingStored"/> is whether the document read has an <c>indexing</c> object; the patch is
    /// conditional on that, so a document whose shape changed since it was read is left alone.
    /// </summary>
    Task<NewsOrganisationDefaultsPatchResult> PatchCreationDefaults(Guid newsOrganisationId, bool indexingStored);

    /// <summary>
    /// Raise <c>latestReleased</c> to the given value only when the stored value is missing or older, in one
    /// conditional patch, so a concurrent writer that already raised it is never lowered. A stored value that is
    /// already as new counts as done. Returns false when the NewsOrganisation no longer exists.
    /// </summary>
    Task<bool> RaiseLatestReleased(Guid newsOrganisationId, DateTime latestReleased);
}
