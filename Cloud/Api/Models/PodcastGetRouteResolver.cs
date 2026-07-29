namespace Api.Models;

/// <summary>
/// Which Azure Function entry handled the HTTP request (App Insights "function" name).
/// </summary>
public enum PodcastGetFunction
{
    PodcastGet,
    PodcastGetWithEpisodeId,
    PodcastGetSlash
}

/// <summary>
/// Which controller method the catch-all continues into (in-process; not a second Functions host invocation).
/// </summary>
public enum PodcastGetContinuation
{
    GetByIdentifier,
    GetWithEpisodeId
}

/// <summary>
/// Resolved podcast-get routing: which function entry ran, how the controller continues, and the
/// request the get-handler / GetAsync service must receive.
/// </summary>
public sealed record PodcastGetRouteResolution(
    PodcastGetFunction InvokedFunction,
    PodcastGetContinuation ContinuesAs,
    PodcastGetRequest HandlerRequest,
    string? EpisodeRoutePodcastSegment = null,
    Guid? EpisodeRouteEpisodeId = null);

/// <summary>
/// Maps podcast GET route shapes to handler requests. Prod often selects PodcastGetSlash for
/// <c>podcast/{guid}/{guid}</c> (see App Insights), so catch-all must produce PodcastId lookups.
/// </summary>
public static class PodcastGetRouteResolver
{
    public static PodcastGetRouteResolution ForSingleSegment(string podcastIdentifier) =>
        new(
            PodcastGetFunction.PodcastGet,
            PodcastGetContinuation.GetByIdentifier,
            PodcastGetRequest.FromRouteIdentifier(podcastIdentifier));

    public static PodcastGetRouteResolution ForNameAndEpisodeId(string podcastName, Guid episodeId) =>
        new(
            PodcastGetFunction.PodcastGetWithEpisodeId,
            PodcastGetContinuation.GetWithEpisodeId,
            PodcastGetRequest.FromRouteIdentifier(podcastName, episodeId),
            podcastName,
            episodeId);

    /// <summary>
    /// <c>podcast/{*podcastIdentifier}</c> (PodcastGetSlash) — used when hosts decode %2F or when
    /// the catch-all wins over the typed two-segment route.
    /// </summary>
    public static PodcastGetRouteResolution ForCatchAll(string podcastIdentifier)
    {
        if (PodcastEpisodePathParser.TrySplitTrailingEpisodeId(
                podcastIdentifier, out var podcastSegment, out var episodeId))
        {
            return new(
                PodcastGetFunction.PodcastGetSlash,
                PodcastGetContinuation.GetWithEpisodeId,
                PodcastGetRequest.FromRouteIdentifier(podcastSegment, episodeId),
                podcastSegment,
                episodeId);
        }

        return new(
            PodcastGetFunction.PodcastGetSlash,
            PodcastGetContinuation.GetByIdentifier,
            PodcastGetRequest.FromRouteIdentifier(podcastIdentifier));
    }
}
