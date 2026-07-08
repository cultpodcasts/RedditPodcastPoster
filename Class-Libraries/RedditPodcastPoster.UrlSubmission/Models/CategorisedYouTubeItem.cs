using RedditPodcastPoster.Episodes.Adapters.Inputs;

namespace RedditPodcastPoster.UrlSubmission.Models;

public sealed record CategorisedYouTubeItem(
    string ShowId,
    string EpisodeId,
    string ShowName,
    string ShowDescription,
    string Publisher,
    string EpisodeTitle,
    string EpisodeDescription,
    DateTime Release,
    TimeSpan Duration,
    Uri? Url,
    bool Explicit,
    Uri? Image,
    string? PlaylistId)
{
    public ResolvedYouTubeItemInput ToAdapterInput() =>
        new(EpisodeId, EpisodeTitle, EpisodeDescription, Release, Duration, Url, Image);
}
