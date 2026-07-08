using RedditPodcastPoster.Episodes.Adapters.Inputs;

namespace RedditPodcastPoster.UrlSubmission.Models;

public sealed record CategorisedAppleItem(
    long? ShowId,
    long? EpisodeId,
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration,
    Uri? Url,
    bool Explicit,
    Uri? Image)
{
    public ResolvedAppleItemInput ToAdapterInput() =>
        new(EpisodeId, EpisodeTitle, EpisodeDescription, Release, Duration, Url, Image);
}
