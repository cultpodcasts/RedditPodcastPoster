using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeEnricher(
    IApplePodcastEnricher applePodcastEnricher,
    IAppleEpisodeResolver appleEpisodeResolver,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<AppleEpisodeEnricher> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IAppleEpisodeEnricher
{
    private static readonly TimeSpan YouTubeAuthorityToAudioReleaseConsiderationThreshold = TimeSpan.FromDays(14);

    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.AppleId == null)
        {
            await applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId != null)
        {
            var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode);
            var ticks = YouTubeAuthorityToAudioReleaseConsiderationThreshold.Ticks;
            if (!string.IsNullOrWhiteSpace(request.Podcast.YouTubePublishingDelayTimeSpan))
            {
                var delay = TimeSpan.Parse(request.Podcast.YouTubePublishingDelayTimeSpan).Ticks;
                if (delay < 0)
                {
                    ticks = Math.Abs(delay);
                }
            }

            var appleItem = await appleEpisodeResolver.FindEpisode(
                findAppleEpisodeRequest,
                indexingContext,
                y => Math.Abs((y.Release - request.Episode.Release).Ticks) < ticks);
            if (appleItem != null && request.Podcast.Episodes.All(x => x.AppleId != appleItem.Id))
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

                if (string.IsNullOrWhiteSpace(request.Episode.Description) &&
                    !string.IsNullOrWhiteSpace(appleItem.Description))
                {
                    request.Episode.Description = appleItem.Description;
                }
            }
        }
    }
}