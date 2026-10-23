using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.UrlSubmission.Languages;

namespace Api.Models;

/// <param name="EpisodeId">The podcast episode, or the news report / TV episode when <paramref name="ContentKind"/> is set.</param>
/// <param name="PodcastId">The podcast, or the news organisation / TV show when <paramref name="ContentKind"/> is set.</param>
/// <param name="ContentKind"><c>NewsReport</c> or <c>TvShowEpisode</c>; null for a podcast episode.</param>
public record DiscoverySubmitItemOutcome(
    Guid DiscoveryItemId,
    Guid? EpisodeId,
    Guid? PodcastId,
    string Message,
    string? Error = null,
    string? ContentKind = null);

public record DiscoverySubmitOutcome(
    string Message,
    bool ErrorsOccurred,
    IReadOnlyList<DiscoverySubmitItemOutcome> Results,
    EntitySearchIndexerResponse SearchIndexer,
    IReadOnlyList<CatalogueLanguageHold>? LanguageDecisionsRequired = null);

public static class DiscoverySubmitItemOutcomeMessages
{
    /// <summary><see cref="DiscoverySubmitItemOutcome.Message"/> for an item whose submit threw.</summary>
    public const string Error = "Error";
}
