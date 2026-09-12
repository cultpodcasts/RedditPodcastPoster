// pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Models; // pragma: allowlist secret

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Series name for non-podcast submits. Publisher is a platform brand on OpenGraph
/// destinations (never a show name) except Vimeo and BitChute, where publisher is the author. // pragma: allowlist secret
/// </summary>
public static class NonPodcastShowNameResolver // pragma: allowlist secret
{
    public static string? TrySeriesName(
        string? showName,
        string? publisher,
        NonPodcastService service) // pragma: allowlist secret
    {
        if (!string.IsNullOrWhiteSpace(showName))
        {
            var resolved = showName.Trim();
            if (!UsesAuthorAsSeries(service) &&
                !string.IsNullOrWhiteSpace(publisher) &&
                string.Equals(resolved, publisher.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return resolved;
        }

        if (UsesAuthorAsSeries(service) &&
            !string.IsNullOrWhiteSpace(publisher))
        {
            return publisher.Trim();
        }

        return null;
    }

    public static string ResolveForCreate(ResolvedNonPodcastServiceItem item) => // pragma: allowlist secret
        TrySeriesName(item.ShowName, item.Publisher, item.NonPodcastService) // pragma: allowlist secret
        ?? item.Title
        ?? string.Empty;

    private static bool UsesAuthorAsSeries(NonPodcastService service) => // pragma: allowlist secret
        service is NonPodcastService.Vimeo or NonPodcastService.BitChute; // pragma: allowlist secret
}
