using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.UrlSubmission.Models;

/// <param name="Episode">The podcast episode created / matched. Null for a news report / TV episode.</param>
/// <param name="ContentKind">
/// <see cref="Categorisation.SubmitClassification.NewsReport"/> or
/// <see cref="Categorisation.SubmitClassification.TvShowEpisode"/> when the channel belongs to a news organisation /
/// TV show; null for a podcast episode.
/// </param>
/// <param name="PlayableId">The news report / TV episode id (with <paramref name="ContentKind"/>).</param>
/// <param name="ParentId">The news organisation / TV show id (with <paramref name="ContentKind"/>).</param>
/// <param name="CatalogueSearchSource">Set when a news report / TV episode was saved: the host indexes it now.</param>
public record DiscoverySubmitResult(
    DiscoverySubmitResultState State,
    Episode? Episode = null,
    string? ContentKind = null,
    Guid? PlayableId = null,
    Guid? ParentId = null,
    CatalogueSearchSource? CatalogueSearchSource = null
);
