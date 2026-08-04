using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

public class ApplePodcastService(
    IAsyncInstance<HttpClient> httpClientProvider,
    ILogger<ApplePodcastService> logger)
    : IApplePodcastService
{
    public async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext)
    {
        logger.LogInformation("{nameofGetEpisodes} podcast-id: '{podcastId}'.", nameof(GetEpisodes), podcastId);
        var appleEpisodes = await GetEpisodes(podcastId, indexingContext, null);

        return appleEpisodes;
    }

    public async Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext)
    {
        var httpClient = await httpClientProvider.GetAsync();
        var requestUri =
            $"/v1/catalog/us/podcast-episodes/{episodeId}?extend=fullDescription&extend[podcasts]=feedUrl&include=podcast";
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failed to request '{requestUri}'. Reason: '{exMessage}', Status-Code: '{statusCode}'.",
                requestUri, ex.Message, ex.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request from '{requestUri}'.", requestUri);
            throw;
        }

        AppleEpisode? appleEpisode = null;

        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            if (appleObject != null && appleObject.Records.Any())
            {
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

                appleEpisode = record.ToAppleEpisode();
            }
        }
        else
        {
            logger.LogError(
                "Failure calling apple-api with url '{requestUri}'. Response-code: '{responseStatusCode}', response-content: '{content}'.",
                requestUri, response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        return appleEpisode;
    }

    private async Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId,
        IndexingContext indexingContext, Func<Record, bool>? breakEvaluator)
    {
        var httpClient = await httpClientProvider.GetAsync();
        var inDescendingDateOrder = true;
        var requestUri = $"/v1/catalog/us/podcasts/{podcastId.PodcastId}/episodes";
        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(requestUri);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failed to request '{requestUri}'. Reason: '{exMessage}', Status-Code: '{statusCode}'.",
                requestUri, ex.Message, ex.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request from '{requestUri}'.", requestUri);
            throw;
        }

        var collectedAppleJson = new List<string>();

        var podcastRecords = new List<Record>();
        if (response.IsSuccessStatusCode)
        {
            var appleJson = await response.Content.ReadAsStringAsync();
            collectedAppleJson.Add(appleJson);
            var appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
            if (appleObject != null && appleObject.Records.Any())
            {
                var lastReleased = appleObject.Records.First().Attributes.Released.Add(TimeSpan.FromSeconds(1));
                foreach (var appleObjectRecord in appleObject.Records)
                {
                    inDescendingDateOrder = lastReleased > appleObjectRecord.Attributes.Released;
                    lastReleased = appleObjectRecord.Attributes.Released;
                    if (!inDescendingDateOrder)
                    {
                        inDescendingDateOrder = false;
                        break;
                    }
                }

                podcastRecords.AddRange(appleObject!.Records);
                while (response.IsSuccessStatusCode &&
                       (breakEvaluator == null || !podcastRecords.Any(breakEvaluator)) &&
                       !string.IsNullOrWhiteSpace(appleObject.Next) &&
                       (
                           !indexingContext.ReleasedSince.HasValue ||
                           podcastRecords.Last().ToAppleEpisode().Release >= indexingContext.ReleasedSince ||
                           !inDescendingDateOrder)
                      )
                {
                    var client = await httpClientProvider.GetAsync();
                    response = await client.GetAsync((string?) appleObject.Next);
                    if (response.IsSuccessStatusCode)
                    {
                        appleJson = await response.Content.ReadAsStringAsync();
                        collectedAppleJson.Add(appleJson);
                        appleObject = JsonSerializer.Deserialize<PodcastResponse>(appleJson);
                        podcastRecords.AddRange(appleObject!.Records);
                    }
                }
            }
        }
        else
        {
            logger.LogError(
                "Failure calling apple-api with url '{requestUri}'. Response-code: '{responseStatusCode}', response-content: '{content}'.",
                requestUri, response.StatusCode, await response.Content.ReadAsStringAsync());
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
