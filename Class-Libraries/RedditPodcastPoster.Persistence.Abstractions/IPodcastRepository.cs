using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IPodcastRepository
{
    Task<Podcast?> GetPodcast(Guid podcastId);
    Task Save(Podcast podcast);
    Task Update(Podcast podcast);
    MergeResult Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge);
    IAsyncEnumerable<Podcast> GetAll();
    Task<IEnumerable<Guid>> GetAllIds();
    Task<Podcast?> GetBy(Func<Podcast, bool> selector);
}