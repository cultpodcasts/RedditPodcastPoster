using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Series name for non-podcast submits. Publisher is a platform brand on OpenGraph
/// destinations (never a show name) except Vimeo, where publisher is the author.
/// </summary>
public static class NonPodcastShowNameResolver
{
    public static string? TrySeriesName(
        string? showName,
        string? publisher,
        NonPodcastService service)
    {
        if (!string.IsNullOrWhiteSpace(showName))
        {
            var resolved = showName.Trim();
            if (service != NonPodcastService.Vimeo &&
                !string.IsNullOrWhiteSpace(publisher) &&
                string.Equals(resolved, publisher.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return resolved;
        }

        if (service == NonPodcastService.Vimeo &&
            !string.IsNullOrWhiteSpace(publisher))
        {
            return publisher.Trim();
        }

        return null;
    }

    public static string ResolveForCreate(ResolvedNonPodcastServiceItem item) =>
        TrySeriesName(item.ShowName, item.Publisher, item.NonPodcastService)
        ?? item.Title
        ?? string.Empty;
}
