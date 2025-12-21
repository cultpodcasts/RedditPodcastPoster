namespace RedditPodcastPoster.Models.Extensions;

public static class PodcastEpisodesExtension
{
    extension(PodcastEpisode podcastEpisode)
    {
        public bool HasMultipleServices()
        {
            var ctr = 0;
            if (podcastEpisode.Podcast.AppleId.HasValue)
            {
                ctr++;
            }

            if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.SpotifyId))
            {
                ctr++;
            }

            if (!string.IsNullOrWhiteSpace(podcastEpisode.Podcast.YouTubeChannelId))
            {
                ctr++;
            }

            return ctr > 1;
        }

        public string ToEpisodeUrl()
        {
            var safePodcastName = podcastEpisode.Podcast.PodcastNameInSafeUrlForm();
            return $"https://cultpodcasts.com/podcast/{safePodcastName}/{podcastEpisode.Episode.Id}";
        }
    }
}