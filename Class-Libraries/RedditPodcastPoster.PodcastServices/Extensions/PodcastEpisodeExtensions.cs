using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Models;

namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeImageUpdateRequest ToEpisodeImageUpdateRequest(this (Podcast Podcast, Episode Episode) podcastEpisode)
    {
        var episode = podcastEpisode.Episode;
        return new EpisodeImageUpdateRequest(
                    !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.SpotifyId) &&
                    !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)) &&
                    EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Spotify) == null,
                    podcastEpisode.Podcast.AppleId != null &&
                    EpisodeServicePresence.AppleEpisodeId(episode) != null &&
                    EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Apple) == null,
                    !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.YouTubeChannelId) &&
                    !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) &&
                    EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube) == null);
    }
}
