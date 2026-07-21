using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.Episodes.Adapters;

internal static class PlatformLinkFactory
{
    internal static PlatformLink? Create(Service service, string? id, Uri? url, Uri? image)
    {
        if (string.IsNullOrWhiteSpace(id) && url is null && image is null)
        {
            return null;
        }

        return new PlatformLink(
            service,
            string.IsNullOrWhiteSpace(id) ? null : id,
            url,
            image);
    }
}
