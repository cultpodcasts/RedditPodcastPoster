using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

/// <summary>
/// Apple adapter: a known episode id is looked up directly (step 0); otherwise the shared
/// <see cref="ICatalogueLookupExecutor"/> plans and runs the strategy chain over <see cref="AppleCandidateSource"/>,
/// exactly as for Spotify. A title-search match is hydrated from amp-api so its fields match feed episodes.
/// </summary>
public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    IAppleShowEpisodePages showEpisodePages,
    IAppleCatalogueLookup catalogueLookup,
    ICatalogueLookupExecutor lookupExecutor,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    public const string Platform = ApplePodcastService.Platform;


    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null,
        CancellationToken cancellationToken = default)
    {
        if (request.PodcastAppleId is not { } podcastAppleId)
        {
            logger.LogInformation(
                "Podcast '{RequestPodcastName}' cannot be found on Apple Podcasts.", request.PodcastName);
            return null;
        }

        if (request.EpisodeAppleId is { } episodeAppleId)
        {
            var direct = await applePodcastService.GetEpisode(
                new ApplePodcastId(podcastAppleId), episodeAppleId, indexingContext, cancellationToken);
            if (direct != null && direct.Id == episodeAppleId)
            {
                return direct;
            }

            // A stale or invalid stored id must not block matching (same rule as Spotify).
        }

        var source = new AppleCandidateSource(request, podcastAppleId, showEpisodePages, catalogueLookup);
        var result = await lookupExecutor.FindMatch(ToLookup(request, indexingContext), source, reducer, cancellationToken);
        if (result.Match == null || !result.FromSearch)
        {
            return result.Match;
        }

        var hydrated = await applePodcastService.GetEpisode(
            new ApplePodcastId(podcastAppleId), result.Match.Id, indexingContext, cancellationToken);
        return hydrated ?? result.Match;
    }

    public static CatalogueLookup ToLookup(FindAppleEpisodeRequest request, IndexingContext indexingContext) =>
        new(
            request.PodcastName,
            request.EpisodeTitle,
            request.Released,
            request.EpisodeLength,
            request.ReleaseAuthority,
            request.EnrichingYouTubeDiscoveredEpisode,
            request.EpisodeDescription,
            request.DefaultSubject,
            request.IgnoredSubjects,
            request.Language,
            request.YouTubePublishingDelay)
        {
            Flow = indexingContext.LookupFlow,
            WindowStart = indexingContext.ReleasedSince,
            ShowEpisodeCount = request.ShowEpisodeCount,
            YouTubePublished = request.YouTubePublished
        };

    /// <summary>
    /// Apple fetch/map adapter: pass-cached amp-api pages, iTunes <c>trackCount</c> as the quick size probe, and
    /// iTunes episode search filtered to the show's Apple id.
    /// </summary>
    private sealed class AppleCandidateSource(
        FindAppleEpisodeRequest request,
        long podcastAppleId,
        IAppleShowEpisodePages showEpisodePages,
        IAppleCatalogueLookup catalogueLookup) : ICatalogueCandidateSource<AppleEpisode>
    {
        public string Platform => AppleEpisodeResolver.Platform;

        public string? ShowId => podcastAppleId.ToString(CultureInfo.InvariantCulture);


        public Task<int?> ProbeShowEpisodeCount(CancellationToken cancellationToken) =>
            catalogueLookup.GetEpisodeCount(podcastAppleId, cancellationToken);

        public Task<CatalogueShowPage<AppleEpisode>?> GetNewestPage(int pageIndex, CancellationToken cancellationToken) =>
            showEpisodePages.GetNewestPage(podcastAppleId, pageIndex, cancellationToken);

        public Task<IReadOnlyList<AppleEpisode>> SearchByTitle(CancellationToken cancellationToken) =>
            catalogueLookup.SearchShowEpisodes(podcastAppleId, request.EpisodeTitle, cancellationToken);

        public string GetId(AppleEpisode item) => item.Id.ToString(CultureInfo.InvariantCulture);

        public Episode ToCatalogueEpisode(AppleEpisode item)
        {
            var mapped = new Episode
            {
                Title = WebUtility.HtmlDecode(item.Title.Trim()),
                Description = item.Description,
                Length = item.Duration,
                ReleaseUtc = item.Release
            };
            EpisodeServicePresence.SetAppleIdentity(mapped, item.Id);
            return mapped;
        }
    }
}
