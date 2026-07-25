using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Logging;

/// <summary>
/// Logs when Spotify marks an episode non-playable for the requested market.
/// Market unavailability is Error (must not be silent); other restrictions stay Warning.
/// </summary>
public static class SpotifyNonPlayableSkipLogger
{
    /// <summary>
    /// Spotify <c>restrictions.reason</c> when the item is not available in the requested market.
    /// </summary>
    public const string MarketRestrictionReason = "market";

    public const string MarketUnavailableMessagePrefix = "Spotify episode not available in market:";

    public const string NonPlayableMessageTemplate =
        "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}, market='{Market}').";

    public const string MarketUnavailableMessageTemplate =
        "Spotify episode not available in market: episode-id='{EpisodeId}' title='{EpisodeName}' market='{Market}' restrictions.reason='{RestrictionReason}'";

    public static bool IsMarketUnavailable(string restrictionReason) =>
        string.Equals(restrictionReason, MarketRestrictionReason, StringComparison.OrdinalIgnoreCase);

    public static void Log(
        ILogger logger,
        SimpleEpisode episode,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(episode);
        Log(logger, episode.Id, episode.Name, episode.GetSpotifyRestrictionReason(), market);
    }

    public static void Log(
        ILogger logger,
        FullEpisode episode,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(episode);
        Log(logger, episode.Id, episode.Name, episode.GetSpotifyRestrictionReason(), market);
    }

    public static void Log(
        ILogger logger,
        string episodeId,
        string episodeName,
        string restrictionReason,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var resolvedMarket = string.IsNullOrWhiteSpace(market) ? Market.CountryCode : market;

        if (IsMarketUnavailable(restrictionReason))
        {
            logger.LogError(
                MarketUnavailableMessageTemplate,
                episodeId,
                episodeName,
                resolvedMarket,
                restrictionReason);
            return;
        }

        logger.LogWarning(
            NonPlayableMessageTemplate,
            episodeId,
            episodeName,
            restrictionReason,
            resolvedMarket);
    }
}
