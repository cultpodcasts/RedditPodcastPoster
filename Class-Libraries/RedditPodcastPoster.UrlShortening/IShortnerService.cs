using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.UrlShortening;

public interface IShortnerService
{
    Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes);
}