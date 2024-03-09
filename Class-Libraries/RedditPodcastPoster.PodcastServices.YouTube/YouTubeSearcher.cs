using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeSearcher : IYouTubeSearcher
{
    public Task<IEnumerable<EpisodeResult>> Search(string query, IndexingContext indexingContext)
    {
        throw new NotImplementedException();
    }
}