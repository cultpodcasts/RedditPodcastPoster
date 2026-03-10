namespace RedditPodcastPoster.PodcastServices.Extensions;

public static class PodcastEpisodeExtensions
{
    public static EpisodeImageUpdateRequest ToEpisodeImageUpdateRequest(this (Models.V2.Podcast Podcast, Models.V2.Episode Episode) podcastEpisode)
    {
        return new EpisodeImageUpdateRequest(
                    !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.SpotifyId) &&
                    !string.IsNullOrWhiteSpace(podcastEpisode.Episode.SpotifyId) &&
                    podcastEpisode.Episode.Images?.Spotify == null,
                    podcastEpisode.Podcast.AppleId != null &&
                    podcastEpisode.Episode.AppleId != null &&
                    podcastEpisode.Episode.Images?.Apple == null,
                    !string.IsNullOrWhiteSpace(podcastEpisode.Podcast.YouTubeChannelId) &&
                    !string.IsNullOrWhiteSpace(podcastEpisode.Episode.YouTubeId) &&
                    podcastEpisode.Episode.Images?.YouTube == null);
    }
}