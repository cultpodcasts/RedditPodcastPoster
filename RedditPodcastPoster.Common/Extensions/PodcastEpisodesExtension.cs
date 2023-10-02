using System.Globalization;
using RedditPodcastPoster.Common.Models;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Extensions;

public static class PodcastEpisodesExtension
{
    public static PostModel ToPostModel(this (Podcast Podcast, IEnumerable<Episode> Episodes) podcastEpisodes)
    {
        var postModel = new PostModel(new PodcastPost(
            podcastEpisodes.Podcast.Name,
            podcastEpisodes.Podcast.TitleRegex,
            podcastEpisodes.Podcast.DescriptionRegex,
            podcastEpisodes.Episodes.Select(ToBasicEpisode),
            podcastEpisodes.Podcast.PrimaryPostService
        ));
        return postModel;

    }
    private static EpisodePost ToBasicEpisode(Episode episode)
    {
        var id = "unknown";
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId))
        {
            id = $"Spotify-{episode.SpotifyId}";
        }
        else if (episode.AppleId != null)
        {
            id = $"Apple-{episode.AppleId}";
        }
        else if (!string.IsNullOrWhiteSpace(episode.YouTubeId))
        {
            id = $"YouTube-{episode.YouTubeId}";
        }

        return new EpisodePost(
            episode.Title,
            episode.Urls.YouTube,
            episode.Urls.Spotify,
            episode.Urls.Apple,
            episode.Release.ToString("dd MMM yyyy"),
            episode.Length.ToString(@"\[h\:mm\:ss\]", CultureInfo.InvariantCulture),
            episode.Description,
            id,
            episode.Release);
    }
}