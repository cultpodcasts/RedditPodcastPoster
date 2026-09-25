namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// Catalogue kind hint for submit v2. Product name is Film, never Movie.
/// <see cref="ContentKind"/> matches the search facet strings.
/// </summary>
public sealed record SubmitClassification(
    string ContentKind,
    bool HasParent,
    bool Reject,
    bool RequiresCurator)
{
    public const string Episode = "Episode";
    public const string TvShowEpisode = "TvShowEpisode";
    public const string Film = "Film";
    public const string NewsReport = "NewsReport";

    public static SubmitClassification PodcastEpisode() =>
        new(Episode, HasParent: true, Reject: false, RequiresCurator: false);
}
