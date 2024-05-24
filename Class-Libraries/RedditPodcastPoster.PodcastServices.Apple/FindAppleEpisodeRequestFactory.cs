using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public static class FindAppleEpisodeRequestFactory
{
    public static FindAppleEpisodeRequest Create(Podcast podcast, Episode episode)
    {
        return new FindAppleEpisodeRequest(
            podcast.AppleId,
            podcast.Name,
            episode.AppleId,
            episode.Title,
            episode.Release,
            podcast.ReleaseAuthority,
            episode.Length
        );
    }

    public static FindAppleEpisodeRequest Create(
        Podcast? podcast,
        iTunesSearch.Library.Models.Podcast applePodcast,
        PodcastServiceSearchCriteria criteria)
    {
        return new FindAppleEpisodeRequest(
            podcast?.AppleId ?? applePodcast.Id,
            applePodcast.Name,
            null,
            criteria.EpisodeTitle,
            criteria.Release,
            podcast?.ReleaseAuthority,
            criteria.Duration);
    }

    public static FindAppleEpisodeRequest Create(long podcastId, long episodeId)
    {
        return new FindAppleEpisodeRequest(
            podcastId,
            string.Empty,
            episodeId,
            string.Empty,
            null,
            null,
            null);
    }
}