using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public interface IAppleItemResolver
{
    Task<iTunesSearch.Library.Models.Podcast?> FindPodcast(Podcast podcast);
    Task<PodcastEpisode> FindEpisode(Podcast podcast, Episode episode);
}