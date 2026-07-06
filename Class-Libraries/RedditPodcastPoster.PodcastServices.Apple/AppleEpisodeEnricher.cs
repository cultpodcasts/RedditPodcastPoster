using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeEnricher(
    IApplePodcastEnricher applePodcastEnricher,
    IAppleEpisodeResolver appleEpisodeResolver,
    IEpisodePlatformMatcher platformMatcher,
    ILogger<AppleEpisodeEnricher> logger)
    : IAppleEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (request.Podcast.IsDelayedYouTubePublishing(request.Episode))
        {
            var timeSpan = request.Podcast.YouTubePublishingDelay().ToString("g");
            logger.LogInformation(
                "'{method}': Bypassing enriching of '{requestEpisodeTitle}' with release-date of '{requestEpisodeRelease:R}' from Apple as it is within the {nameof(request.Podcast.YouTubePublishingDelay)} which is '{timeSpan}'.",
                nameof(Enrich), request.Episode.Title, request.Episode.Release,
                nameof(request.Podcast.YouTubePublishingDelay), timeSpan);
            return;
        }

        if (request.Podcast.AppleId == null)
        {
            await applePodcastEnricher.AddId(request.Podcast);
        }

        if (request.Podcast.AppleId != null)
        {
            var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(request.Podcast, request.Episode);
            var probeEpisode = new Episode
            {
                Title = request.Episode.Title,
                Length = request.Episode.Length,
                Release = findAppleEpisodeRequest.Released ?? request.Episode.Release
            };
            var assignedAppleIds = request.Episodes
                .Where(x => x.AppleId is > 0)
                .Select(x => x.AppleId!.Value)
                .ToHashSet();

            var appleItem = await appleEpisodeResolver.FindEpisode(
                findAppleEpisodeRequest,
                indexingContext,
                y => !assignedAppleIds.Contains(y.Id) &&
                     findAppleEpisodeRequest.Released.HasValue &&
                     platformMatcher.CatalogueReleaseMatches(
                         probeEpisode,
                         new Episode
                         {
                             Title = y.Title,
                             Length = y.Duration,
                             Release = y.Release,
                             AppleId = y.Id
                         },
                         request.Podcast));
            if (appleItem != null && request.Episodes.All(x => x.AppleId != appleItem.Id))
            {
                var url = appleItem.Url.CleanAppleUrl();
                request.Episode.Urls.Apple = url;
                request.Episode.AppleId = appleItem.Id;
                logger.LogInformation(
                    "Episode.Release.TimeOfDay: '{ReleaseTimeOfDay:G}' podcast-id '{PodcastId}' with episode with apple-id '{AppleItemId}'."
                    , request.Episode.Release.TimeOfDay, request.Podcast.Id, appleItem.Id);
                // Spotify release metadata is date-only; Apple typically publishes simultaneously and carries time-of-day.
                if (request.Episode.Release.TimeOfDay == TimeSpan.Zero &&
                    DateOnly.FromDateTime(request.Episode.Release) ==
                    DateOnly.FromDateTime(appleItem.Release) &&
                    !EpisodeReleaseMatchTolerance.ShouldPreserveYouTubeAuthoritativeRelease(
                        request.Podcast, request.Episode))
                {
                    logger.LogInformation(
                        "Updating Episode.Release.TimeOfDay with: '{AppleItemRelease:G}'.", appleItem.Release);
                    request.Episode.Release = appleItem.Release;
                    enrichmentContext.Release = appleItem.Release;
                }

                enrichmentContext.Apple = url;

                if (string.IsNullOrWhiteSpace(request.Episode.Description) &&
                    !string.IsNullOrWhiteSpace(appleItem.Description))
                {
                    request.Episode.Description = appleItem.Description;
                }

                var image = appleItem.Image;
                if (image != null)
                {
                    request.Episode.Images ??= new EpisodeImages();
                    request.Episode.Images.Apple = image;
                }
            }
        }
    }
}