using RedditPodcastPoster.Episodes.Domain;

namespace RedditPodcastPoster.Episodes.Adapters;

internal static class ReleaseInfoFactory
{
    internal static ReleaseInfo SpotifyRelease(DateTime release) =>
        new(release.Date, ReleasePrecision.DateOnly);

    internal static ReleaseInfo DateTimeUtcRelease(DateTime release) =>
        new(release, ReleasePrecision.DateTimeUtc);
}
