using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Search;

namespace CreateSearchIndex;

public partial class CreateSearchIndexProcessor(
    SearchClient searchClient,
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    ISearchIndexerService searchIndexerService,
    IOptions<CosmosDbSettingsV2> cosmosDbSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CreateSearchIndexProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private const string ActiveEpisodesFilter =
        "((NOT IS_DEFINED(e.podcastRemoved)) OR e.podcastRemoved=false) and ((NOT IS_DEFINED(e.removed)) OR e.removed=false)";

    private static readonly Regex Whitespace = CreateWhitespaceRegex();
    private static readonly TimeSpan IndexAtMinutes = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Frequency = TimeSpan.FromMinutes(30);
    private readonly CosmosDbSettingsV2 _cosmosDbSettings = cosmosDbSettings.Value;

    public async Task Process(CreateSearchIndexRequest request)
    {
        if (request.TearDownIndex)
        {
            await TearDown(request);
        }

        if (!string.IsNullOrWhiteSpace(request.IndexName))
        {
            var existingIndex = await TryGetIndex(request.IndexName);
            if (existingIndex == null)
            {
                var result = await CreateIndex(request);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DataSourceName))
        {
            var existingDataSource = await TryGetDataSource(request.DataSourceName);
            if (existingDataSource == null)
            {
                var result = await CreateDataSource(request);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.IndexerName) &&
            !string.IsNullOrWhiteSpace(request.DataSourceName) &&
            !string.IsNullOrWhiteSpace(request.IndexName))
        {
            var existingIndexer = await TryGetIndexer(request.IndexerName);
            if (existingIndexer == null)
            {
                var result = await CreateIndexer(request);
            }
        }

        if (request.RunIndexer)
        {
            await LogCosmosEpisodeCounts();
            await LogPotentialDuplicatePair();
            await RunIndexerWithRetries(request);
        }
    }

    private async Task RunIndexerWithRetries(CreateSearchIndexRequest request)
    {
        var executedAttempts = 0;
        long totalDocsSucceeded = 0;
        long? previousIndexedDocumentCount = null;

        void LogFinalSummary(string terminalReason)
        {
            logger.LogInformation(
                "Indexer aggregate summary: Attempts={Attempts}; TotalDocsSucceeded={TotalDocsSucceeded}; TerminalReason={TerminalReason}",
                executedAttempts,
                totalDocsSucceeded,
                terminalReason);
        }

        if (string.IsNullOrWhiteSpace(request.IndexerName))
        {
            logger.LogWarning(
                "Run-indexer requested without --indexer name. Running single trigger without monitoring.");
            var x = await searchIndexerService.RunIndexer();
            logger.LogInformation("Indexer trigger result: {IndexerState}.", x.IndexerState);
            LogFinalSummary($"NoIndexerName:{x.IndexerState}");
            return;
        }

        var maxAttempts = Math.Max(1, request.RunIndexerMaxAttempts);
        var pollInterval = TimeSpan.FromSeconds(Math.Max(2, request.RunIndexerPollSeconds));

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            logger.LogInformation(
                "Indexer attempt {Attempt}/{MaxAttempts}: triggering indexer '{IndexerName}'.",
                attempt,
                maxAttempts,
                request.IndexerName);

            var attemptTriggeredAtUtc = DateTimeOffset.UtcNow;
            var runState = await searchIndexerService.RunIndexer();
            logger.LogInformation(
                "Indexer attempt {Attempt}/{MaxAttempts}: trigger state = {IndexerState}.",
                attempt,
                maxAttempts,
                runState.IndexerState);

            var requiresNewRunCorrelation = runState.IndexerState == IndexerState.Executed;
            var minRunStartUtc = requiresNewRunCorrelation
                ? attemptTriggeredAtUtc.AddSeconds(-1)
                : (DateTimeOffset?)null;

            var status = await WaitForIndexerCompletion(request.IndexerName, pollInterval, minRunStartUtc);
            var result = GetCorrelatedExecutionResult(status, minRunStartUtc);
            if (result == null)
            {
                executedAttempts++;
                logger.LogError(
                    "Indexer attempt {Attempt}/{MaxAttempts}: no correlated execution result was found; stopping automation.",
                    attempt,
                    maxAttempts);
                LogFinalSummary("NoCorrelatedExecutionResult");
                throw new InvalidOperationException(
                    $"Indexer attempt {attempt}/{maxAttempts} completed without a correlated execution result for indexer '{request.IndexerName}'.");
            }

            executedAttempts++;
            totalDocsSucceeded += result.ItemCount;
            logger.LogInformation(
                "Indexer attempt {Attempt}/{MaxAttempts} completed. Status={Status}; DocsSucceeded={ItemCount}; Errors={FailedCount}; StartTime={StartTime}; EndTime={EndTime}; Message={Message}",
                attempt,
                maxAttempts,
                result.Status,
                result.ItemCount,
                result.FailedItemCount,
                result.StartTime,
                result.EndTime,
                result.ErrorMessage ?? string.Empty);

            try
            {
                var currentIndexedDocumentCount = await GetIndexedDocumentCount();
                if (previousIndexedDocumentCount.HasValue)
                {
                    var deltaSincePrevious = currentIndexedDocumentCount - previousIndexedDocumentCount.Value;
                    logger.LogInformation(
                        "Indexer attempt {Attempt}/{MaxAttempts} index count: CurrentTotal={CurrentTotal}; DeltaSincePrevious={DeltaSincePrevious}",
                        attempt,
                        maxAttempts,
                        currentIndexedDocumentCount,
                        deltaSincePrevious);

                    if (deltaSincePrevious <= 0)
                    {
                        throw new InvalidOperationException(
                            $"Indexer attempt {attempt}/{maxAttempts} did not increase index document count. Previous={previousIndexedDocumentCount.Value}; Current={currentIndexedDocumentCount}.");
                    }
                }
                else
                {
                    logger.LogInformation(
                        "Indexer attempt {Attempt}/{MaxAttempts} index count: CurrentTotal={CurrentTotal}; Baseline=true",
                        attempt,
                        maxAttempts,
                        currentIndexedDocumentCount);
                }

                previousIndexedDocumentCount = currentIndexedDocumentCount;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Indexer attempt {Attempt}/{MaxAttempts}: unable to query index document count.",
                    attempt,
                    maxAttempts);
            }

            if (result.Status == IndexerExecutionStatus.Success)
            {
                logger.LogInformation(
                    "Indexer completed successfully on attempt {Attempt}/{MaxAttempts}.",
                    attempt,
                    maxAttempts);
                LogFinalSummary("Success");
                return;
            }

            if (!IsRetryableFailure(result))
            {
                logger.LogError(
                    "Indexer failed with non-retryable status on attempt {Attempt}/{MaxAttempts}. Stopping reruns.",
                    attempt,
                    maxAttempts);
                LogFinalSummary($"NonRetryableFailure:{result.Status}");
                throw new InvalidOperationException(
                    $"Indexer attempt {attempt}/{maxAttempts} failed with status '{result.Status}'. {result.ErrorMessage}");
            }
        }

        logger.LogWarning(
            "Indexer reached max attempts ({MaxAttempts}) and still has retryable completion status.",
            maxAttempts);
        LogFinalSummary("MaxAttemptsReachedWithRetryableFailure");
        throw new InvalidOperationException(
            $"Indexer '{request.IndexerName}' reached max attempts ({maxAttempts}) with retryable failures.");
    }

    private async Task<SearchIndexerStatus> WaitForIndexerCompletion(
        string indexerName,
        TimeSpan pollInterval,
        DateTimeOffset? minRunStartUtc = null)
    {
        while (true)
        {
            var statusResponse = await searchIndexerClient.GetIndexerStatusAsync(indexerName);
            var status = statusResponse.Value;
            var result = GetCorrelatedExecutionResult(status, minRunStartUtc);
            if (result == null)
            {
                await Task.Delay(pollInterval);
                continue;
            }

            if (result.Status == IndexerExecutionStatus.InProgress ||
                result.Status == IndexerExecutionStatus.Reset)
            {
                await Task.Delay(pollInterval);
                continue;
            }

            return status;
        }
    }

    private static IndexerExecutionResult? GetCorrelatedExecutionResult(
        SearchIndexerStatus status,
        DateTimeOffset? minRunStartUtc)
    {
        IndexerExecutionResult? latest = null;

        void Consider(IndexerExecutionResult? candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (minRunStartUtc.HasValue)
            {
                if (!candidate.StartTime.HasValue || candidate.StartTime.Value < minRunStartUtc.Value)
                {
                    return;
                }
            }

            if (latest == null ||
                (candidate.StartTime ?? DateTimeOffset.MinValue) > (latest.StartTime ?? DateTimeOffset.MinValue))
            {
                latest = candidate;
            }
        }

        Consider(status.LastResult);
        foreach (var execution in status.ExecutionHistory)
        {
            Consider(execution);
        }

        return latest;
    }

    private async Task<Azure.Response<SearchIndexer>> CreateIndexer(CreateSearchIndexRequest request)
    {
        var nextIndex = DateTimeOffset.Now
            .Add(Frequency)
            .Floor(Frequency)
            .Add(IndexAtMinutes);

        var indexingSchedule = new IndexingSchedule(Frequency) { StartTime = nextIndex };
        var searchIndexer = new SearchIndexer(request.IndexerName, request.DataSourceName, request.IndexName)
        {
            Schedule = indexingSchedule,
            Description = string.Empty,
            Parameters = new IndexingParameters
            {
                MaxFailedItems = 0,
                MaxFailedItemsPerBatch = 0,
                Configuration =
                {
                    { "assumeOrderByHighWaterMarkColumn", true }
                }
            }
        };
        return await searchIndexerClient.CreateOrUpdateIndexerAsync(searchIndexer);
    }

    private async Task<Azure.Response<SearchIndexerDataSourceConnection>> CreateDataSource(
        CreateSearchIndexRequest request)
    {
        var connectionString =
            $"AccountEndpoint={_cosmosDbSettings.Endpoint};Database={_cosmosDbSettings.DatabaseId};AccountKey={_cosmosDbSettings.AuthKeyOrResourceToken}";
        var query = @$"SELECT
                            e.id,
                            e.title as episodeTitle,
                            e.podcastName as podcastName,
                            SUBSTRING (e.description, 0, {Constants.DescriptionSize}) as episodeDescription,
                            e.release,
                            e.duration,
                            e.explicit,
                            e.urls.spotify != null ? e.urls.spotify : e.urls.spotify.x as spotify,
                            e.urls.apple != null ? e.urls.apple : e.urls.apple.x as apple,
                            e.urls.youtube != null ? e.urls.youtube : e.urls.youtube.x as youtube,
                            e.urls.bbc != null ? e.urls.bbc : e.urls.bbc.x as bbc,
                            e.urls.internetArchive != null ? e.urls.internetArchive : e.urls.internetArchive.x as internetArchive,
                            e.subjects as subjects,
                            e.podcastSearchTerms as podcastSearchTerms,
                            e.searchTerms as episodeSearchTerms,
                            e.images.youtube ?? e.images.spotify ?? e.images.apple ?? e.images.other as image,
                            e.lang ?? e.podcastLanguage ?? e.lang.x as lang,
                            e._ts
                            FROM episodes e
                            WHERE {ActiveEpisodesFilter}
                              and e._ts >= @HighWaterMark
                            ORDER BY e._ts";
        query = Whitespace.Replace(query, " ").Trim();
        var searchIndexDataContainer = new SearchIndexerDataContainer(_cosmosDbSettings.EpisodesContainer)
        {
            Query = query
        };
        var searchIndexDataSourceConnection = new SearchIndexerDataSourceConnection(request.DataSourceName,
            SearchIndexerDataSourceType.CosmosDb, connectionString, searchIndexDataContainer)
        {
            DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy("_ts")
        };

        return await searchIndexerClient.CreateOrUpdateDataSourceConnectionAsync(searchIndexDataSourceConnection);
    }

    private async Task<Azure.Response<SearchIndex>> CreateIndex(CreateSearchIndexRequest request)
    {
        var index = new SearchIndex(request.IndexName)
        {
            Fields = new FieldBuilder
            {
                Serializer = new JsonObjectSerializer(new JsonSerializerOptions
                    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            }.Build(typeof(EpisodeSearchRecord)),
            DefaultScoringProfile = string.Empty,
            CorsOptions = new CorsOptions(["*"]) { MaxAgeInSeconds = 300 }
        };
        return await searchIndexClient.CreateOrUpdateIndexAsync(index);
    }

    private async Task TearDown(CreateSearchIndexRequest request)
    {
        Response result;
        if (!string.IsNullOrWhiteSpace(request.IndexerName))
        {
            result = await searchIndexerClient.DeleteIndexerAsync(request.IndexerName);
            if (result.Status != (int)HttpStatusCode.NoContent || result.IsError)
            {
                throw new InvalidOperationException($"Unable to tear-down indexer '{request.IndexerName}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.IndexName))
        {
            result = await searchIndexClient.DeleteIndexAsync(request.IndexName);
            if (result.Status != (int)HttpStatusCode.NoContent || result.IsError)
            {
                throw new InvalidOperationException($"Unable to tear-down index '{request.IndexName}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.DataSourceName))
        {
            result = await searchIndexerClient.DeleteDataSourceConnectionAsync(request.DataSourceName);
            if (result.Status != (int)HttpStatusCode.NoContent || result.IsError)
            {
                throw new InvalidOperationException($"Unable to tear-down data-source '{request.DataSourceName}'.");
            }
        }

        logger.LogWarning(
            "Ensure that the indexer has run and completed indexing ALL records. The indexer will time-out at 10,000 records, so it must be re-run until all records are re-indexed.");
    }

    private async Task<SearchIndex?> TryGetIndex(string indexName)
    {
        try
        {
            var response = await searchIndexClient.GetIndexAsync(indexName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<SearchIndexerDataSourceConnection?> TryGetDataSource(string dataSourceName)
    {
        try
        {
            var response = await searchIndexerClient.GetDataSourceConnectionAsync(dataSourceName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<SearchIndexer?> TryGetIndexer(string indexerName)
    {
        try
        {
            var response = await searchIndexerClient.GetIndexerAsync(indexerName);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static bool IsRetryableFailure(IndexerExecutionResult result)
    {
        return IsTimeoutResult(result) || IsQuotaResult(result);
    }

    private static bool IsTimeoutResult(IndexerExecutionResult result)
    {
        if (result.Status != IndexerExecutionStatus.TransientFailure)
        {
            return false;
        }

        var message = result.ErrorMessage ?? string.Empty;
        return message.Contains("time", StringComparison.OrdinalIgnoreCase) &&
               message.Contains("out", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQuotaResult(IndexerExecutionResult result)
    {
        if (result.Status != IndexerExecutionStatus.TransientFailure)
        {
            return false;
        }

        var message = result.ErrorMessage ?? string.Empty;
        return message.Contains("quota", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<long> GetIndexedDocumentCount()
    {
        var response = await searchClient.GetDocumentCountAsync();
        return response.Value;
    }

    private async Task LogCosmosEpisodeCounts()
    {
        using var cosmosClient = CreateCosmosClient();
        var container = cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);

        var totalEpisodes = await QueryCosmosCount(container, "SELECT VALUE COUNT(1) FROM episodes e");
        var matchingIndexerFilter = await QueryCosmosCount(container,
            $"SELECT VALUE COUNT(1) FROM episodes e WHERE {ActiveEpisodesFilter}");

        logger.LogInformation(
            "Cosmos episode counts: TotalEpisodes={TotalEpisodes}; MatchingIndexerFilter={MatchingIndexerFilter}",
            totalEpisodes,
            matchingIndexerFilter);
    }

    private async Task LogPotentialDuplicatePair()
    {
        using var cosmosClient = CreateCosmosClient();
        var container = cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);

        var query = $@"SELECT e.id, e.podcastId, e.title, e.release, e.spotifyId, e.appleId, e.youTubeId, e.podcastName
                       FROM episodes e
                       WHERE {ActiveEpisodesFilter}";
        var iterator = container.GetItemQueryIterator<EpisodeDuplicateSample>(new QueryDefinition(query));
        var seen = new Dictionary<string, EpisodeDuplicateSample>(StringComparer.Ordinal);

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var item in response)
            {
                var fingerprint = CreateDuplicateFingerprint(item);
                if (string.IsNullOrWhiteSpace(fingerprint))
                {
                    continue;
                }

                if (seen.TryGetValue(fingerprint, out var existing) && existing.Id != item.Id)
                {
                    logger.LogWarning(
                        "Potential duplicate episode pair in Cosmos: Fingerprint={Fingerprint}; First={FirstId}/{FirstPodcastId}/{FirstPodcastName}/{FirstTitle}/{FirstRelease}; Second={SecondId}/{SecondPodcastId}/{SecondPodcastName}/{SecondTitle}/{SecondRelease}",
                        fingerprint,
                        existing.Id,
                        existing.PodcastId,
                        existing.PodcastName ?? string.Empty,
                        existing.Title,
                        existing.Release,
                        item.Id,
                        item.PodcastId,
                        item.PodcastName ?? string.Empty,
                        item.Title,
                        item.Release);
                    return;
                }

                seen[fingerprint] = item;
            }
        }

        logger.LogInformation("No potential duplicate episode pair was found in Cosmos for the current filter.");
    }

    private static string? CreateDuplicateFingerprint(EpisodeDuplicateSample item)
    {
        if (!string.IsNullOrWhiteSpace(item.SpotifyId))
        {
            return $"spotify:{item.SpotifyId}";
        }

        if (item.AppleId.HasValue)
        {
            return $"apple:{item.AppleId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(item.YouTubeId))
        {
            return $"youtube:{item.YouTubeId}";
        }

        if (string.IsNullOrWhiteSpace(item.PodcastId) || string.IsNullOrWhiteSpace(item.Title) ||
            !item.Release.HasValue)
        {
            return null;
        }

        return $"fallback:{item.PodcastId}|{item.Title.Trim().ToUpperInvariant()}|{item.Release.Value:O}";
    }

    private CosmosClient CreateCosmosClient()
    {
        var options = new CosmosClientOptions();
        if (_cosmosDbSettings.UseGateway == true)
        {
            options.ConnectionMode = ConnectionMode.Gateway;
        }

        return new CosmosClient(_cosmosDbSettings.Endpoint, _cosmosDbSettings.AuthKeyOrResourceToken, options);
    }

    private static async Task<long> QueryCosmosCount(Container container, string queryText)
    {
        var iterator = container.GetItemQueryIterator<long>(new QueryDefinition(queryText));
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            foreach (var count in response)
            {
                return count;
            }
        }

        return 0;
    }

    [GeneratedRegex(@"\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex CreateWhitespaceRegex();

    private sealed class EpisodeDuplicateSample
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("podcastId")]
        public string PodcastId { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("release")]
        public DateTime? Release { get; set; }

        [JsonPropertyName("spotifyId")]
        public string? SpotifyId { get; set; }

        [JsonPropertyName("appleId")]
        public long? AppleId { get; set; }

        [JsonPropertyName("youTubeId")]
        public string? YouTubeId { get; set; }

        [JsonPropertyName("podcastName")]
        public string? PodcastName { get; set; }
    }
}