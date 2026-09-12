using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Series name for non-podcast submits. Publisher is a platform brand on OpenGraph
/// destinations (never a show name) except Vimeo and BcVideo, where publisher is the author.
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
            var resolvedPublisher = publisher.Trim();
            if (IsCatalogDisplayName(service, resolvedPublisher))
            {
                return null;
            }

            return resolvedPublisher;
        }

        return null;
    }

    public static string ResolveForCreate(ResolvedNonPodcastServiceItem item) =>
        TrySeriesName(item.ShowName, item.Publisher, item.NonPodcastService)
        ?? item.Title
        ?? string.Empty;

    private static bool UsesAuthorAsSeries(NonPodcastService service) =>
        service is NonPodcastService.Vimeo or NonPodcastService.BcVideo;

    private static bool IsCatalogDisplayName(NonPodcastService service, string name)
    {
        var key = service switch
        {
            NonPodcastService.Vimeo => ServiceKeys.Vimeo,
            NonPodcastService.BcVideo => ServiceKeys.BcVideo,
            _ => null
        };
        return key != null
            && ServiceCatalog.TryGet(key, out var descriptor)
            && string.Equals(name, descriptor.DisplayName, StringComparison.OrdinalIgnoreCase);
    }
}
