using RedditPodcastPoster.Common.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleEpisodeResolver
{
    Task<PodcastEpisode?> FindEpisode(FindAppleEpisodeRequest request);
}