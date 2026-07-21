using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes;

/// <summary>
/// Emits a stable Warning-level provenance line for new episode creates so App Insights
/// can answer submit-url vs indexer (and service) by episode-id or URL substring.
/// Warning is intentional: Information is heavily sampled in production.
/// </summary>
public static class EpisodeCreationLogger
{
    public const string MessagePrefix = "Episode created:";

    public const string MessageTemplate =
        "Episode created: episode-id='{EpisodeId}' title='{Title}' podcast-id='{PodcastId}' source='{Source}' caller='{Caller}' service='{Service}' spotify-id='{SpotifyId}' spotify-url='{SpotifyUrl}' apple-id='{AppleId}' apple-url='{AppleUrl}' youtube-id='{YouTubeId}' youtube-url='{YouTubeUrl}'";

    /// <summary>
    /// Logs episode create provenance.
    /// </summary>
    /// <param name="caller">
    /// Creating call site — who invoked <see cref="LogCreated"/> (the caller), e.g.
    /// <c>PodcastUpdater.Update</c> or <c>CategorisedItemProcessor.ProcessCategorisedItem</c>.
    /// Named <c>caller</c> deliberately: in C# logging this matches <c>CallerMemberName</c>
    /// semantics; not the callee (<see cref="LogCreated"/> itself).
    /// </param>
    public static void LogCreated(
        ILogger logger,
        Episode episode,
        Guid podcastId,
        EpisodeCreationSource source,
        Service service,
        string caller)
    {
        logger.LogWarning(
            MessageTemplate,
            episode.Id,
            episode.Title,
            podcastId,
            source,
            caller,
            service,
            EmptyToNull(episode.SpotifyId),
            episode.Urls.Spotify,
            episode.AppleId,
            episode.Urls.Apple,
            EmptyToNull(episode.YouTubeId),
            episode.Urls.YouTube);
    }

    /// <summary>
    /// Same content as the rendered Warning message (for unit tests / docs).
    /// </summary>
    /// <param name="caller">Creating call site (caller of <see cref="LogCreated"/>), e.g. <c>PodcastUpdater.Update</c>.</param>
    public static string FormatMessage(
        Episode episode,
        Guid podcastId,
        EpisodeCreationSource source,
        Service service,
        string caller)
    {
        return
            $"{MessagePrefix} episode-id='{episode.Id}' title='{episode.Title}' podcast-id='{podcastId}' source='{source}' caller='{caller}' service='{service}' spotify-id='{EmptyToNull(episode.SpotifyId)}' spotify-url='{episode.Urls.Spotify}' apple-id='{episode.AppleId}' apple-url='{episode.Urls.Apple}' youtube-id='{EmptyToNull(episode.YouTubeId)}' youtube-url='{episode.Urls.YouTube}'";
    }

    /// <summary>
    /// Which platform supplied the create: sole present identity, else release authority when present on the episode, else first available.
    /// </summary>
    public static Service ResolveCreatingService(Episode episode, Service? releaseAuthority = null)
    {
        var hasSpotify = !string.IsNullOrWhiteSpace(episode.SpotifyId);
        var hasYouTube = !string.IsNullOrWhiteSpace(episode.YouTubeId);
        var hasApple = episode.AppleId is > 0;

        var presentCount = (hasSpotify ? 1 : 0) + (hasYouTube ? 1 : 0) + (hasApple ? 1 : 0);
        if (presentCount == 1)
        {
            if (hasSpotify)
            {
                return Service.Spotify;
            }

            if (hasYouTube)
            {
                return Service.YouTube;
            }

            return Service.Apple;
        }

        if (releaseAuthority is Service.Spotify or Service.YouTube or Service.Apple)
        {
            var authority = releaseAuthority.Value;
            if ((authority == Service.Spotify && hasSpotify) ||
                (authority == Service.YouTube && hasYouTube) ||
                (authority == Service.Apple && hasApple))
            {
                return authority;
            }
        }

        if (hasSpotify)
        {
            return Service.Spotify;
        }

        if (hasYouTube)
        {
            return Service.YouTube;
        }

        if (hasApple)
        {
            return Service.Apple;
        }

        return releaseAuthority ?? Service.Other;
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
