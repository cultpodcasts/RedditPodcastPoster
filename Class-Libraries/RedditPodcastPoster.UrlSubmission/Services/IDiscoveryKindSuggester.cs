using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Suggest-only kind hints for an unmapped discovery item. Never files the item; the curator must confirm.
/// </summary>
public interface IDiscoveryKindSuggester
{
    /// <summary>
    /// Returns a prompt when the item looks like a TMDB film (title and runtime) or matches an existing
    /// film, TV show or news report. Null when the channel is already mapped, or nothing worth offering.
    /// </summary>
    Task<DiscoveryKindHold?> SuggestAsync(DiscoveryResult discoveryResult, CancellationToken cancellationToken = default);
}