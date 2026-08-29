using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Bluesky.Logging;

/// <summary>
/// Emits a stable Warning-level line when an episode is posted to Bluesky
/// (or when a curator requests BlueskyPosted without a network post) so App Insights
/// can answer provenance by episode-id. Warning is intentional: Information is sampled away.
/// </summary>
public static class BlueskyPostLogger
{
    public const string PostedMessagePrefix = "Bluesky posted:";
    public const string FlagSetMessagePrefix = "BlueskyPosted flag set:";

    public const string PostedMessageTemplate =
        "Bluesky posted: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' podcast-name='{PodcastName}' caller='{Caller}' posted-url='{PostedUrl}' posted-service='{PostedService}' catalog-urls='{CatalogUrls}'";

    public const string FlagSetMessageTemplate =
        "BlueskyPosted flag set: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' caller='{Caller}' network-post='false'";

    public static void LogPosted(
        ILogger logger,
        PodcastEpisode podcastEpisode,
        string caller)
    {
        var (postedUrl, postedService, catalogUrls) = PostedUrlFields(podcastEpisode.Episode);
        logger.LogWarning(
            PostedMessageTemplate,
            podcastEpisode.Episode.Id,
            podcastEpisode.Episode.Title,
            podcastEpisode.Podcast.Id,
            podcastEpisode.Podcast.Name,
            caller,
            postedUrl,
            postedService,
            catalogUrls);
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
        var (postedUrl, postedService, catalogUrls) = PostedUrlFields(podcastEpisode.Episode);
        return
            $"{PostedMessagePrefix} episode-id='{podcastEpisode.Episode.Id}' title='{podcastEpisode.Episode.Title}' podcast-id='{podcastEpisode.Podcast.Id}' podcast-name='{podcastEpisode.Podcast.Name}' caller='{caller}' posted-url='{postedUrl}' posted-service='{postedService}' catalog-urls='{catalogUrls}'";
    }

    private static (Uri? PostedUrl, string PostedService, string CatalogUrls) PostedUrlFields(Episode episode)
    {
        var catalogUrls = EpisodeServicePresence.FormatCatalogUrlsForLog(episode);
        if (EpisodeServicePresence.TryGetPreferredSocialPost(
                episode, out var url, out var serviceKey, out _))
        {
            return (url, serviceKey, catalogUrls);
        }

        return (null, "", catalogUrls);
    }
}
