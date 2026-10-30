using System.Linq.Expressions;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// Podcast episodes only. A <see cref="PodcastEpisode"/> with <see cref="PodcastEpisode.IsCataloguePlayable"/> set
/// (a TV episode, news report or film projected for social posting) carries a projection <see cref="Episode"/>
/// that must never be saved here: it would write a fake episode into the Episodes container. Callers that take a
/// <see cref="PodcastEpisode"/> and save its episode must route catalogue playables to their own repository
/// (as <c>ICataloguePlayableSocialPosts</c> does) or reject them.
/// </summary>
public interface IEpisodeRepository : IRepository<Episode>, IFilterableRepository<Episode>
{
    Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId);
    Task<int> Count(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId);
    IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId, Expression<Func<Episode, bool>> selector);
    Task<Episode?> GetMostRecentByPodcastId(Guid podcastId);
    /// <summary>Podcast episodes only; never a catalogue projection (see the interface remarks).</summary>
    Task Save(IEnumerable<Episode> episodes);
    Task Delete(Guid podcastId, Guid episodeId);

    /// <summary>
    /// Bulk delete for a podcast kind transfer, which deletes the podcast itself afterwards. Deletes run with
    /// bounded parallelism; a missing episode is skipped (so a resumed transfer is safe). Unlike
    /// <see cref="Delete"/>, it does not read each episode first and does not recompute the podcast's latest
    /// release (the podcast is about to go), and it adjusts the home page episode count once, by the number of
    /// deleted episodes that were counted (from the <paramref name="episodes"/> snapshot).
    /// </summary>
    /// <returns>The number of episodes actually deleted.</returns>
    Task<int> DeleteForParentTransfer(Guid podcastId, IReadOnlyCollection<Episode> episodes);

    /// <summary>
    /// Surgical Cosmos patch of <c>/guests</c> only. Does not touch handle fields or other properties.
    /// </summary>
    Task PatchGuests(Guid podcastId, Guid episodeId, string[] guests);
}