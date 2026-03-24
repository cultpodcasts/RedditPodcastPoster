using System.Linq.Expressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence.Legacy;

public interface IPodcastRepository
{
    Task<Podcast?> GetPodcast(Guid podcastId);
    Task Save(Podcast podcast);
    MergeResult Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge);
    IAsyncEnumerable<Podcast> GetAll();
    IAsyncEnumerable<Guid> GetAllIds();
    Task<int> GetTotalCount();
    IAsyncEnumerable<string> GetAllFileKeys();
    Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector);
    Task<T?> GetBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item);
    IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector);
    IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item);
    Task<IEnumerable<Guid>> GetPodcastsIdsWithUnpostedReleasedSince(DateTime since);
    Task<IEnumerable<Guid>> GetPodcastIdsWithUntweetedReleasedSince(DateTime since);
    Task<IEnumerable<Guid>> GetPodcastIdsWithBlueskyReadyReleasedSince(DateTime since);
    Task<bool> PodcastHasEpisodesAwaitingEnrichment(Guid podcastId, DateTime since);
}
