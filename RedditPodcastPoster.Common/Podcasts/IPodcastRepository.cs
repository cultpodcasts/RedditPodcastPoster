using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public interface IPodcastRepository
{
    Task<Podcast?> GetPodcast(string key);
    Task Save(Podcast podcast);
    Task Update(Podcast podcast);
    MergeResult Merge(Podcast podcast, IEnumerable<Episode> episodesToMerge);
    IAsyncEnumerable<Podcast> GetAll();
}