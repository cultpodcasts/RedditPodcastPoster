namespace RedditPodcastPoster.UrlSubmission.Enrichers;

/// <summary>
/// Episode enricher that overwrites title, description, release, length, and service
/// image/URL for non-podcast services when re-submitting with refresh-meta (CLI <c>-r</c>).
/// </summary>
public interface IRefreshMetaEpisodeEnricher : IEpisodeEnricher;
