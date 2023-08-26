using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleEpisodeResolver
{
    Task<PodcastEpisode> FindEpisode(Podcast podcast, Episode episode);
}