using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.Domain;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Enriching;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.YouTube.Enrichment;

public class YouTubeEpisodeEnricher(
    IYouTubeServiceWrapper youTubeService,
    IYouTubeItemResolver youTubeItemResolver,
    ITextSanitiser textSanitiser,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    IEpisodePlatformApplier episodePlatformApplier,
    IEpisodeCatalogueAdapter<YouTubeCatalogueInput> youTubeAdapter,
    IPlatformEnrichmentApplicator enrichmentApplicator,
    ILogger<YouTubeEpisodeEnricher> logger)
    : PlatformEpisodeEnricherTemplate(enrichmentApplicator), IYouTubeEpisodeEnricher
{
    public async Task Enrich(
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext)
    {
        if (IsBypassedByDelayedYouTubePublishing(request, "YouTube", logger))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(request.Episode.YouTubeId) && request.Episode.Urls.YouTube == null)
        {
            ApplyYouTubeLink(
                request.Episode,
                request.Episode.YouTubeId,
                SearchResultExtensions.ToYouTubeUrl(request.Episode.YouTubeId),
                enrichmentContext);
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Episode.YouTubeId) && request.Episode.Urls.YouTube != null)
        {
            var videoId = YouTubeIdResolver.Extract(request.Episode.Urls.YouTube);
            if (videoId != null)
            {
                request.Episode.YouTubeId = enrichmentContext.YouTubeId = videoId;
            }

            return;
        }

        var youTubeItem = await youTubeItemResolver.FindEpisode(request, indexingContext);
        if (youTubeItem?.SearchResult != null)
        {
            if (!string.IsNullOrWhiteSpace(youTubeItem.SearchResult.Id.VideoId) &&
                request.Episodes.All(x => x.YouTubeId != youTubeItem.SearchResult.Id.VideoId))
            {
                await EnrichFromCatalogueItem(
                    youTubeItem.SearchResult.Id.VideoId,
                    youTubeItem.SearchResult.Snippet.PublishedAtDateTimeOffset,
                    request,
                    indexingContext,
                    enrichmentContext,
                    youTubeItem.SearchResult.Snippet.Title,
                    youTubeItem.SearchResult.ToYouTubeUrl());
            }
        }
        else if (youTubeItem?.PlaylistItem != null)
        {
            if (!string.IsNullOrWhiteSpace(youTubeItem.PlaylistItem.Snippet.ResourceId.VideoId) &&
                request.Episodes.All(x => x.YouTubeId != youTubeItem.PlaylistItem.Snippet.ResourceId.VideoId))
            {
                await EnrichFromCatalogueItem(
                    youTubeItem.PlaylistItem.Snippet.ResourceId.VideoId,
                    youTubeItem.PlaylistItem.Snippet.PublishedAtDateTimeOffset,
                    request,
                    indexingContext,
                    enrichmentContext,
                    youTubeItem.PlaylistItem.Snippet.Title,
                    youTubeItem.PlaylistItem.Snippet.ToYouTubeUrl());
            }
        }
    }

    private async Task EnrichFromCatalogueItem(
        string episodeYouTubeId,
        DateTimeOffset? release,
        EnrichmentRequest request,
        IndexingContext indexingContext,
        EnrichmentContext enrichmentContext,
        string title,
        Uri url)
    {
        if (release != null)
        {
            logger.LogInformation(
                "'{method}': Found matching YouTube episode: '{episodeYouTubeId}' with title '{title}' and release-date '{releaseValueUtcDateTime:R}'.",
                nameof(EnrichFromCatalogueItem), episodeYouTubeId, title, release.Value.UtcDateTime);
        }
        else
        {
            logger.LogInformation(
                "'{method}': Found matching YouTube episode: '{episodeYouTubeId}' with title '{title}'.",
                nameof(EnrichFromCatalogueItem), episodeYouTubeId, title);
        }

        var catalogueInput = new YouTubeCatalogueInput(
            episodeYouTubeId,
            title,
            string.Empty,
            request.Episode.Length,
            release?.UtcDateTime ?? request.Episode.Release,
            url,
            null);
        ApplyResolvedCandidate(request, youTubeAdapter.Adapt(catalogueInput), enrichmentContext);
        enrichmentContext.YouTubeId = episodeYouTubeId;

        if (string.IsNullOrWhiteSpace(request.Episode.Description) || request.Episode.Images?.YouTube == null)
        {
            await ApplyYouTubeVideoDetails(
                episodeYouTubeId,
                url,
                request,
                indexingContext);
        }

        if ((request.Podcast.AppleId == null || request.Episode.AppleId == null) &&
            request.Episode.Release.TimeOfDay == TimeSpan.Zero &&
            release.HasValue &&
            episodePlatformApplier.ApplyFillMissingRelease(
                request.Episode,
                release.Value.UtcDateTime))
        {
            enrichmentContext.Release = release.Value.UtcDateTime;
        }
    }

    private async Task ApplyYouTubeVideoDetails(
        string episodeYouTubeId,
        Uri url,
        EnrichmentRequest request,
        IndexingContext indexingContext)
    {
        var videoContentDetails =
            await youTubeVideoService.GetVideoContentDetails(youTubeService, [episodeYouTubeId],
                indexingContext, true);
        var item = videoContentDetails?.FirstOrDefault();
        if (item == null)
        {
            return;
        }

        var rawDescription = item.Snippet.Description.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(rawDescription))
        {
            var description = !string.IsNullOrWhiteSpace(request.Podcast.DescriptionRegex)
                ? textSanitiser.SanitiseDescription(
                    rawDescription,
                    new Regex(request.Podcast.DescriptionRegex))
                : rawDescription;
            EnrichmentApplicator.ApplyDescription(request.Episode, description);
        }

        var image = await youTubeThumbnailResolver.GetImageUrlAsync(item);
        if (image != null)
        {
            EnrichmentApplicator.ApplySupplementalLink(
                request.Episode,
                new PlatformLink(Service.YouTube, episodeYouTubeId, url, image));
        }
    }

    private void ApplyYouTubeLink(
        RedditPodcastPoster.Models.Episode episode,
        string youTubeId,
        Uri url,
        EnrichmentContext enrichmentContext)
    {
        if (episodePlatformApplier.ApplyFillMissing(
                episode,
                new EpisodePlatformPatch(
                    new PlatformLink(Service.YouTube, youTubeId, url, null),
                    null,
                    null)))
        {
            enrichmentContext.YouTube = url;
        }
    }
}

