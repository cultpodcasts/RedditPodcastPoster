using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeEnricher : IAppleEpisodeEnricher
{
    private static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly IApplePodcastEnricher _applePodcastEnricher;
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

    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.AppleId == null)
        {
            await _applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId != null)
        {
            var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode);
            var appleItem = await _appleEpisodeResolver.FindEpisode(
                findAppleEpisodeRequest,
                indexingContext,
                y =>
                    Math.Abs((y.Release - request.Episode.Release).Ticks) <
                    YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks
            );
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