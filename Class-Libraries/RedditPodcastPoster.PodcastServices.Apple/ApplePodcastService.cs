using System.Text.Json;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

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
                var itemsWithDuration = appleObject.Records.Where(x => x.Attributes.Duration > TimeSpan.Zero);
                if (!itemsWithDuration.Any())
                {
                    logger.LogError(
                        "Failure calling apple-api with url '{requestUri}'. No item returned for podcast-episode-query for episode-id '{episodeId}' with duration > 0.",
                        requestUri, episodeId);
                    return null;
                }

                if (itemsWithDuration.Count() > 1)
                {
                    logger.LogError(
                        "Failure calling apple-api with url '{requestUri}'. Multiple items returned for podcast-episode-query for episode-id '{episodeId}' with duration > 0.",
                        requestUri, episodeId);
                    return null;
                }

                appleEpisode = itemsWithDuration.Single().ToAppleEpisode();
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

        var appleEpisodes = podcastRecords
            .Where(x => x.Attributes.Duration > TimeSpan.Zero)
            .Select(x => x.ToAppleEpisode()).ToArray();
        if (podcastRecords.Any() && !appleEpisodes.Any())
        {
            logger.LogError(
                "Missing duration-attribute on all apple-podcast episodes for podcast with apple-podcast-id '{podcastId}'. podcast-records count:'{podcastRecordsCount}',  apple-episodes count:'{appleEpisodesCount}'.",
                podcastId.PodcastId, podcastRecords.Count, appleEpisodes.Count());
            foreach (var json in collectedAppleJson)
            {
                logger.LogError(json);
            }
        }
        else
        {
            if (podcastRecords.Count > 0)
            {
                logger.LogInformation(
                    "Successfully found podcast-episodes with duration. Apple-podcast-id '{podcastId}', items-with-duration: '{appleEpisodesCount}/{podcastRecordsCount}'.",
                    podcastId.PodcastId, appleEpisodes.Count(), podcastRecords.Count);
            }
        }

        return appleEpisodes;
    }
}