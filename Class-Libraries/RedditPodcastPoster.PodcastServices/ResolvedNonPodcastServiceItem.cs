using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices;

public record ResolvedNonPodcastServiceItem(
    Podcast? Podcast = null,
    Episode? Episode = null,
    Uri? Url = null,
    string? Title = null,
    string? Publisher = null,
    bool? IsBBC = false,
    bool? IsInternetArchive = false,
    Uri? Image= null
)
{
    public TimeSpan Duration = TimeSpan.Zero;
    public DateTime Release => DateTime.MinValue;
    public bool Explicit => false;
    public string EpisodeDescription => string.Empty;
    public Uri? BBCUrl => IsBBC != null && IsBBC.Value ? Url : null;
    public Uri? InternetArchiveUrl => IsInternetArchive != null && IsInternetArchive.Value ? Url : null;
    public Uri? Image { get; init; }
}