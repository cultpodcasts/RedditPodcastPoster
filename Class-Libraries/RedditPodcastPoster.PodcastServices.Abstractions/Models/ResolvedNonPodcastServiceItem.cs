using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public record ResolvedNonPodcastServiceItem(
    StreamingService StreamingService,
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
    public Uri? BBCUrl => StreamingServiceWire.IsBbc(StreamingService) ? Url : null;
    public Uri? InternetArchiveUrl => StreamingService == StreamingService.InternetArchive ? Url : null;
}
