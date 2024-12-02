using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public record ResolvedNonPodcastServiceItem(
    Podcast? Podcast = null,
    Episode? Episode = null,
    Uri? Url = null,
    string? Title = null,
    string? Description = null,
    string? Publisher = null,
    bool? IsBBC = false,
    bool? IsInternetArchive = false,
    Uri? Image = null,
    DateTime? Release = null,
    TimeSpan? Duration = null
)
{
    public bool Explicit => false;
    public Uri? BBCUrl => IsBBC != null && IsBBC.Value ? Url : null;
    public Uri? InternetArchiveUrl => IsInternetArchive != null && IsInternetArchive.Value ? Url : null;
}