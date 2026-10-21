using System.Text.Json;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

public class ApplePodcastService(
    IAsyncInstance<HttpClient> httpClientProvider,
    ICatalogueRequestGuard requestGuard,
    ILogger<ApplePodcastService> logger)
    : IApplePodcastService
{
    public const string Platform = CataloguePlatforms.Apple;

    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("{nameofGetEpisodes} podcast-id: '{podcastId}'.", nameof(GetEpisodes), podcastId);
        var appleEpisodes = await GetEpisodes(podcastId, indexingContext, null, cancellationToken);

        return appleEpisodes;
    }

    public async Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext, CancellationToken cancellationToken = default)
    {
        var requestUri =
            $"/v1/catalog/us/podcast-episodes/{episodeId}?extend=fullDescription&extend[podcasts]=feedUrl&include=podcast";
        var appleJson = await GetJson(requestUri, $"GetEpisode episode='{episodeId}'", cancellationToken);
        if (appleJson == null)
        {
            return null;
        }

        var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
        if (appleObject == null || !appleObject.Records.Any())
        {
            return null;
        }

        // Apple occasionally omits durationInMilliseconds (catalogue bug). Keep the
        // episode so title/release/subject matching can still attach; do not require Duration > 0.
        if (appleObject.Records.Count > 1)
        {
            logger.LogError(
                "Failure calling apple-api with url '{requestUri}'. Multiple items returned for podcast-episode-query for episode-id '{episodeId}'.",
                requestUri, episodeId);
            return null;
        }

        var record = appleObject.Records.Single();
        if (record.Attributes.Duration <= TimeSpan.Zero)
        {
            logger.LogWarning(
                "Apple episode '{episodeId}' has no duration; keeping for title/release matching.",
                episodeId);
        }

        return record.ToAppleEpisode();
    }

    public async Task<AppleEpisodePage?> GetEpisodePage(ApplePodcastId podcastId, string? next, CancellationToken cancellationToken = default)
    {
        var requestUri = string.IsNullOrWhiteSpace(next) ? EpisodesUri(podcastId) : next;
        var appleJson = await GetJson(requestUri, $"GetEpisodePage podcast='{podcastId.PodcastId}'", cancellationToken);
        if (appleJson == null)
        {
            return null;
        }

        var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
        return new AppleEpisodePage(
            appleObject?.Records.Select(x => x.ToAppleEpisode()).ToList() ?? [],
            string.IsNullOrWhiteSpace(appleObject?.Next) ? null : appleObject.Next);
    }

    private static string EpisodesUri(ApplePodcastId podcastId) => $"/v1/catalog/us/podcasts/{podcastId.PodcastId}/episodes";

    /// <summary>
    /// One amp-api request behind the fixed timeout guard. Returns null (and logs) on HTTP failure, a non-success
    /// status or when the guard fires, so callers keep whatever they already collected.
    /// </summary>
    private async Task<string?> GetJson(string requestUri, string operation, CancellationToken cancellationToken)
    {
        var httpClient = await httpClientProvider.GetAsync();
        return await requestGuard.Run<string>(Platform, operation, async ct =>
        {
            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(requestUri, ct);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex,
                    "Failed to request '{requestUri}'. Reason: '{exMessage}', Status-Code: '{statusCode}'.",
                    requestUri, ex.Message, ex.StatusCode);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError(
                    "Failure calling apple-api with url '{requestUri}'. Response-code: '{responseStatusCode}', response-content: '{content}'.",
                    requestUri, response.StatusCode, await response.Content.ReadAsStringAsync(ct));
                return null;
            }

            return await response.Content.ReadAsStringAsync(ct);
        }, cancellationToken);
    }

    private async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId,
        IndexingContext indexingContext, Func<Record, bool>? breakEvaluator, CancellationToken cancellationToken)
    {
        var appleJson = await GetJson(EpisodesUri(podcastId), $"GetEpisodes podcast='{podcastId.PodcastId}'", cancellationToken);
        if (appleJson == null)
        {
            return null;
        }

        var collectedAppleJson = new List<string> { appleJson };
        var podcastRecords = new List<Record>();
        var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
        if (appleObject != null && appleObject.Records.Any())
        {
            // Equal release timestamps still count as newest-first (non-increasing). A strict
            // greater-than probe used to flip ascending on same-day episodes and disable
            // ReleasedSince early-stop — walking entire high-volume catalogues on MatchOtherServices.
            var newestFirst = AppleCataloguePagination.IsNewestFirst(
                appleObject.Records.Select(r => r.Attributes.Released).ToList());

            podcastRecords.AddRange(appleObject.Records);
            var pagesFetchedAfterFirst = 0;
            while ((breakEvaluator == null || !podcastRecords.Any(breakEvaluator)) &&
                   AppleCataloguePagination.ShouldContinuePaging(
                       !string.IsNullOrWhiteSpace(appleObject.Next),
                       indexingContext.ReleasedSince,
                       podcastRecords.Last().ToAppleEpisode().Release,
                       newestFirst,
                       pagesFetchedAfterFirst))
            {
                var nextJson = await GetJson(appleObject.Next, $"GetEpisodes podcast='{podcastId.PodcastId}' next", cancellationToken);
                if (nextJson == null)
                {
                    // Failed or timed out: keep the pages already collected.
                    break;
                }

                collectedAppleJson.Add(nextJson);
                appleObject = JsonSerializer.Deserialize<PodcastResponse>(nextJson)!;
                podcastRecords.AddRange(appleObject.Records);
                pagesFetchedAfterFirst++;
            }

            if (!newestFirst &&
                indexingContext.ReleasedSince.HasValue &&
                pagesFetchedAfterFirst >= AppleCataloguePagination.MaxPages &&
                !string.IsNullOrWhiteSpace(appleObject.Next))
            {
                logger.LogError(
                    AppleCataloguePagination.CircuitBreakerTrippedMessageTemplate,
                    pagesFetchedAfterFirst,
                    AppleCataloguePagination.MaxPages,
                    indexingContext.ReleasedSince,
                    appleObject.Next);
            }
        }

        // Keep episodes even when Apple omits durationInMilliseconds. Matching uses title /
        // release / subjects when length is missing; filtering them out emptied catalogues
        // (Aug 2026 Unpacking 1619) while Apple still returned the correct episode ids.
        var appleEpisodes = podcastRecords.Select(x => x.ToAppleEpisode()).ToArray();
        var withDurationCount = appleEpisodes.Count(x => x.Duration > TimeSpan.Zero);
        if (podcastRecords.Count > 0 && withDurationCount == 0)
        {
            logger.LogWarning(
                "Missing duration-attribute on all apple-podcast episodes for podcast with apple-podcast-id '{podcastId}'. podcast-records count:'{podcastRecordsCount}'. Keeping episodes for title/release matching.",
                podcastId.PodcastId, podcastRecords.Count);
            foreach (var json in collectedAppleJson)
            {
                logger.LogWarning(json);
            }
        }
        else if (podcastRecords.Count > 0)
        {
            logger.LogInformation(
                "Successfully found podcast-episodes. Apple-podcast-id '{podcastId}', items-with-duration: '{withDurationCount}/{podcastRecordsCount}'.",
                podcastId.PodcastId, withDurationCount, podcastRecords.Count);
        }

        return appleEpisodes;
    }
}
