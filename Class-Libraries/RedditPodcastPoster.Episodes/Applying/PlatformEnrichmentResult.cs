using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Applying;

public sealed record PlatformEnrichmentResult(
    bool Updated,
    Service? Service,
    Uri? PlatformUrl,
    bool ReleaseUpdated,
    DateTime? Release)
{
    public static PlatformEnrichmentResult None { get; } =
        new(false, null, null, false, null);
}
