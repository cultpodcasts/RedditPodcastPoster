namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// Inputs the submit classifier is allowed to use. Scrapers supply the booleans;
/// this type does not fetch pages.
/// </summary>
public sealed record SubmitClassificationSignals(
    Uri Url,
    bool PodcastServiceEpisode = false,
    bool MadeAsFilm = false,
    bool Series = false,
    bool News = false,
    bool YouTubeNewsStation = false,
    bool CuratorConfirmed = false);
