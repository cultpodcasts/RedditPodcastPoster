using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Bluesky.Logging;

/// <summary>
/// Emits a stable Warning-level line when an episode is posted to Bluesky
/// (or when the BlueskyPosted flag is set without a network post) so App Insights
/// can answer provenance by episode-id. Warning is intentional: Information is sampled away.
/// </summary>
public static class BlueskyPostLogger
{
    public const string PostedMessagePrefix = "Bluesky posted:";
    public const string FlagSetMessagePrefix = "BlueskyPosted flag set:";

    public const string PostedMessageTemplate =
        "Bluesky posted: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' podcast-name='{PodcastName}' caller='{Caller}' spotify-url='{SpotifyUrl}' youtube-url='{YouTubeUrl}' apple-url='{AppleUrl}'";

    public const string FlagSetMessageTemplate =
        "BlueskyPosted flag set: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' caller='{Caller}' network-post='false'";

    public static void LogPosted(
        ILogger logger,
        PodcastEpisode podcastEpisode,
        string caller)
    {
        logger.LogWarning(
            PostedMessageTemplate,
            podcastEpisode.Episode.Id,
            podcastEpisode.Episode.Title,
            podcastEpisode.Podcast.Id,
            podcastEpisode.Podcast.Name,
            caller,
            podcastEpisode.Episode.Urls.Spotify,
            podcastEpisode.Episode.Urls.YouTube,
            podcastEpisode.Episode.Urls.Apple);
    }

    public static void LogFlagSetWithoutPost(
        ILogger logger,
        Episode episode,
        Guid podcastId,
        string caller)
    {
        logger.LogWarning(
            FlagSetMessageTemplate,
            episode.Id,
            episode.Title,
            podcastId,
            caller);
    }

    public static string FormatPostedMessage(
        PodcastEpisode podcastEpisode,
        string caller)
    {
        return
            $"{PostedMessagePrefix} episode-id='{podcastEpisode.Episode.Id}' title='{podcastEpisode.Episode.Title}' podcast-id='{podcastEpisode.Podcast.Id}' podcast-name='{podcastEpisode.Podcast.Name}' caller='{caller}' spotify-url='{podcastEpisode.Episode.Urls.Spotify}' youtube-url='{podcastEpisode.Episode.Urls.YouTube}' apple-url='{podcastEpisode.Episode.Urls.Apple}'";
    }
}
