namespace RedditPodcastPoster.Search.Models;

/// <summary>
/// Facet values on the unified playable index. ADR-0003.
/// </summary>
public static class SearchContentKind
{
    public const string Episode = "Episode";
    public const string TvShowEpisode = "TvShowEpisode";
    public const string Film = "Film";
    public const string NewsReport = "NewsReport";
}
