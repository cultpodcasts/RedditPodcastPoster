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
    Task<Podcast?> GetBy(Expression<Func<Podcast, bool>> selector);
    IAsyncEnumerable<Podcast> GetAllBy(Expression<Func<Podcast, bool>> selector);
    IAsyncEnumerable<T> GetAllBy<T>(Expression<Func<Podcast, bool>> selector, Expression<Func<Podcast, T>> item);
}