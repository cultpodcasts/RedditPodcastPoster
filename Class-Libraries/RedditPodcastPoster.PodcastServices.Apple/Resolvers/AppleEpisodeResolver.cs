using System.Globalization;
using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

/// <summary>
/// Apple adapter over the shared <see cref="ICatalogueCandidatePolicy"/>: a known episode id is looked up directly;
/// otherwise the policy sources candidates from the show feed and matches them exactly as for Spotify.
/// This class only maps Apple types.
/// </summary>
public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    ICatalogueCandidatePolicy candidatePolicy,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
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
                new ApplePodcastId(podcastAppleId), episodeAppleId, indexingContext);
            if (direct != null && direct.Id == episodeAppleId)
            {
                return direct;
            }

            // A stale or invalid stored id must not block matching (same rule as Spotify).
        }

        var source = new AppleCandidateSource(new ApplePodcastId(podcastAppleId), indexingContext, applePodcastService);
        return await candidatePolicy.FindMatch(ToLookup(request), source, reducer);
    }

    private static CatalogueLookup ToLookup(FindAppleEpisodeRequest request) =>
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
            request.YouTubePublishingDelay);

    /// <summary>
    /// Apple fetch/map adapter. The show feed is the pass-cached podcast episode list, already one fetch, so the
    /// recent stage returns the whole list and matching runs in a single pass (no ranking split between the
    /// newest page and older episodes). Apple has no episode title search, so the search fallback is the same
    /// list; the policy skips candidates already tried, so it only adds anything for releases outside the
    /// recent window, where the feed stage is skipped.
    /// </summary>
    private sealed class AppleCandidateSource(
        ApplePodcastId podcastId,
        IndexingContext indexingContext,
        ICachedApplePodcastService service) : ICatalogueCandidateSource<AppleEpisode>
    {
        public string Platform => "Apple";

        public bool HasShow => true;

        public Task<IReadOnlyList<AppleEpisode>> GetRecentShowEpisodes(int pageSize, CancellationToken cancellationToken) =>
            GetAll();

        public Task<IReadOnlyList<AppleEpisode>> SearchEpisodes(CancellationToken cancellationToken) => GetAll();

        public string GetId(AppleEpisode item) => item.Id.ToString(CultureInfo.InvariantCulture);

        public Episode ToCatalogueEpisode(AppleEpisode item)
        {
            var mapped = new Episode
            {
                Title = WebUtility.HtmlDecode(item.Title.Trim()),
                Description = item.Description ?? string.Empty,
                Length = item.Duration,
                ReleaseUtc = item.Release
            };
            EpisodeServicePresence.SetAppleIdentity(mapped, item.Id);
            return mapped;
        }

        private async Task<IReadOnlyList<AppleEpisode>> GetAll()
        {
            var episodes = await service.GetEpisodes(podcastId, indexingContext);
            return episodes?.OrderByDescending(x => x.Release).ToList() ?? [];
        }
    }
}
