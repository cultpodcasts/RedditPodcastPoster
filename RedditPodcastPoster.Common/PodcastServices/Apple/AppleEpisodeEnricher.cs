using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeEnricher : IAppleEpisodeEnricher
{
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly ILogger<AppleEpisodeEnricher> _logger;

    public AppleEpisodeEnricher(
        IApplePodcastEnricher applePodcastEnricher,
        IAppleEpisodeResolver appleEpisodeResolver,
        ILogger<AppleEpisodeEnricher> logger)
    {
        _applePodcastEnricher = applePodcastEnricher;
        _appleEpisodeResolver = appleEpisodeResolver;
        _logger = logger;
    }

    public async Task Enrich(EnrichmentRequest request, IndexingContext indexingContext, EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.AppleId == null)
        {
            await _applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId != null)
        {
            var appleItem =
                await _appleEpisodeResolver.FindEpisode(
                    FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode), indexingContext);
            if (appleItem != null)
            {
                var url = appleItem.Url.CleanAppleUrl();
                request.Episode.Urls.Apple = url;
                request.Episode.AppleId = appleItem.Id;
                if (request.Episode.Release.TimeOfDay == TimeSpan.Zero)
                {
                    request.Episode.Release = appleItem.Release;
                    enrichmentContext.Release = appleItem.Release;
                }

                enrichmentContext.Apple = url;
            }
        }
    }
}