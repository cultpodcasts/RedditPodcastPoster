using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Extensions;

public static class PlatformEnrichmentResultExtensions
{
    public static void ApplyTo(this PlatformEnrichmentResult result, EnrichmentContext enrichmentContext)
    {
        if (result.PlatformUrl is not null && result.Service is not null)
        {
            switch (result.Service)
            {
                case Service.Spotify:
                    enrichmentContext.Spotify = result.PlatformUrl;
                    break;
                case Service.Apple:
                    enrichmentContext.Apple = result.PlatformUrl;
                    break;
                case Service.YouTube:
                    enrichmentContext.YouTube = result.PlatformUrl;
                    break;
            }
        }

        if (result.ReleaseUpdated && result.Release.HasValue)
        {
            enrichmentContext.Release = result.Release.Value;
        }
    }
}
