using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public record ResolvedNonPodcastServiceItem(
    NonPodcastService NonPodcastService,
    Podcast? Podcast = null,
    Episode? Episode = null,
    Uri? Url = null,
    string? Title = null,
    string? Description = null,
    string? Publisher = null,
    Uri? Image = null,
    DateTime? Release = null,
    TimeSpan? Duration = null,
    bool? KnownExplicit = false
)
{
    public bool Explicit => KnownExplicit ?? false;
    public Uri? BBCUrl => NonPodcastService == NonPodcastService.BBC ? Url : null;
    public Uri? InternetArchiveUrl => NonPodcastService == NonPodcastService.InternetArchive ? Url : null;
}