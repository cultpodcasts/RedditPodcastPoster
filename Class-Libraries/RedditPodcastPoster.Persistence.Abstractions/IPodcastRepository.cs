using System.Linq.Expressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPodcastRepository
{
    Task<Podcast?> GetPodcast(Guid podcastId);
    Task Save(Podcast podcast);
    Task Update(Podcast podcast);
    MergeResult Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge);
    IAsyncEnumerable<Podcast> GetAll();
    IAsyncEnumerable<Guid> GetAllIds();
    IAsyncEnumerable<string> GetAllFileKeys();
    Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector);
    Task<T?> GetBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item);
    IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector);
    IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item);
    Task<IEnumerable<Guid>> GetPodcastsIdsWithUnpostedReleasedSince(DateTime since);
    Task<IEnumerable<Guid>> GetPodcastIdsWithUntweetedReleasedSince(DateTime since);
    Task<IEnumerable<Guid>> GetPodcastIdsWithBlueskyReadyReleasedSince(DateTime since);
}