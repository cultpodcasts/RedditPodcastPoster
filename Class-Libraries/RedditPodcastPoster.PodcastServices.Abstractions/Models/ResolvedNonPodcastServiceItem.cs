using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record ResolvedNonPodcastServiceItem(
    StreamingService? StreamingService = null,
    Podcast? Podcast = null,
    Episode? Episode = null,
    Uri? Url = null,
    string? Title = null,
    string? Description = null,
    string? Publisher = null,
    Uri? Image = null,
    DateTime? Release = null,
    TimeSpan? Duration = null,
    bool? KnownExplicit = false,
    string? ShowName = null,
    bool MadeAsFilm = false
)
{
    public bool Explicit => KnownExplicit ?? false;
    public Uri? BBCUrl => StreamingService is { } known && StreamingServiceWire.IsBbc(known) ? Url : null;
    public Uri? InternetArchiveUrl => StreamingService is RedditPodcastPoster.Models.Podcasts.StreamingService.InternetArchive ? Url : null;
}
