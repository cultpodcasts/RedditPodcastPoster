using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    IEpisodePlatformMatcher platformMatcher,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode>? podcastEpisodes = null;
        if (request.PodcastAppleId.HasValue)
        {
            var applePodcastId = new ApplePodcastId(request.PodcastAppleId.Value);
            if (request.EpisodeAppleId.HasValue)
            {
                var episode =
                    await applePodcastService.GetEpisode(applePodcastId, request.EpisodeAppleId.Value, indexingContext);
                if (episode != null)
                {
                    podcastEpisodes = [episode];
                }
            }
            else
            {
                podcastEpisodes = await applePodcastService.GetEpisodes(applePodcastId, indexingContext);
            }
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null && request.PodcastAppleId.HasValue)
        {
            var probe = CreateProbeEpisode(request);
            var candidates = podcastEpisodes.Select(ToCatalogueEpisode).ToList();
            Func<Episode, bool>? episodeReducer = reducer == null
                ? null
                : e =>
                {
                    var source = podcastEpisodes.FirstOrDefault(x => x.Id == e.AppleId);
                    return source != null && reducer(source);
                };

            var match = platformMatcher.FindCatalogueMatchByLength(
                probe,
                candidates,
                CreateLookupPodcast(request),
                episodeMatchRegex: null,
                new CatalogueMatchByLengthOptions(
                    request.ReleaseAuthority,
                    AcceptUniqueDurationWithoutTitleMatch: false,
                    request.EnrichingYouTubeDiscoveredEpisode),
                episodeReducer);

            matchingEpisode = match == null
                ? null
                : podcastEpisodes.FirstOrDefault(x => x.Id == match.AppleId);
        }
        else if (matchingEpisode == null && podcastEpisodes != null && !request.PodcastAppleId.HasValue)
        {
            logger.LogInformation(
                "Podcast '{RequestPodcastName}' cannot be found on Apple Podcasts.", request.PodcastName);
        }

        return matchingEpisode;
    }

    private static Episode CreateProbeEpisode(FindAppleEpisodeRequest request) =>
        new()
        {
            Title = WebUtility.HtmlDecode(request.EpisodeTitle.Trim()),
            Length = request.EpisodeLength ?? TimeSpan.Zero,
            Release = request.Released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(AppleEpisode episode) =>
        new()
        {
            Title = WebUtility.HtmlDecode(episode.Title.Trim()),
            Length = episode.Duration,
            Release = episode.Release,
            AppleId = episode.Id
        };

    private static Podcast CreateLookupPodcast(FindAppleEpisodeRequest request) =>
        new()
        {
            ReleaseAuthority = request.ReleaseAuthority ?? Service.Apple,
            YouTubePublicationOffset = request.YouTubePublishingDelay?.Ticks
        };
}
