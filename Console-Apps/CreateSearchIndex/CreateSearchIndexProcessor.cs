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
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Search.Services;

namespace CreateSearchIndex;

public partial class CreateSearchIndexProcessor(
    SearchClient searchClient,
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    ISearchIndexerService searchIndexerService,
    IOptions<CosmosDbSettings> cosmosDbSettings,
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
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

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
            var duplicates= await LogPotentialDuplicatePair();
            if (duplicates && !request.NotBreakOnDuplicates)
            {
                var message = "Potential duplicate episode pairs were found in Cosmos matching the indexer filter. Break-on-duplicates is enabled, so stopping the indexer run to prevent potential data issues. Please investigate and resolve duplicates before re-running.";
                var ex= new InvalidOperationException(message);
                logger.LogError(ex, message);
                throw ex;
            }
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
        var maxWaitDuration = TimeSpan.FromSeconds(Math.Max(1, request.RunIndexerMaxWaitSeconds));

        logger.LogInformation(
            "Indexer monitor configuration: MaxAttempts={MaxAttempts}; PollInterval={PollInterval}; MaxWaitPerAttempt={MaxWaitPerAttempt}",
            maxAttempts,
            pollInterval,
            maxWaitDuration);

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

            var status = await WaitForIndexerCompletion(request.IndexerName, pollInterval, minRunStartUtc, maxWaitDuration);
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

            if (result.Status == IndexerExecutionStatus.InProgress ||
                result.Status == IndexerExecutionStatus.Reset)
            {
                executedAttempts++;
                logger.LogWarning(
                    "Indexer attempt {Attempt}/{MaxAttempts}: max wait exceeded with indexer still reporting {Status}; treating as retryable stall.",
                    attempt,
                    maxAttempts,
                    result.Status);
                if (attempt < maxAttempts)
                {
                    continue;
                }

                LogFinalSummary($"MaxAttemptsReachedWithStallStatus:{result.Status}");
                throw new InvalidOperationException(
                    $"Indexer '{request.IndexerName}' reached max attempts ({maxAttempts}) stalled with status '{result.Status}'.");
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
        DateTimeOffset? minRunStartUtc = null,
        TimeSpan? maxWaitDuration = null)
    {
        var deadline = maxWaitDuration.HasValue
            ? DateTimeOffset.UtcNow.Add(maxWaitDuration.Value)
            : (DateTimeOffset?)null;

        while (true)
        {
            var statusResponse = await searchIndexerClient.GetIndexerStatusAsync(indexerName);
            var status = statusResponse.Value;
            var result = GetCorrelatedExecutionResult(status, minRunStartUtc);
            if (result == null)
            {
                if (deadline.HasValue && DateTimeOffset.UtcNow >= deadline.Value)
                {
                    logger.LogWarning(
                        "WaitForIndexerCompletion: max wait of {MaxWaitDuration} exceeded without finding a correlated execution result; returning current status.",
                        maxWaitDuration);
                    return status;
                }

                await Task.Delay(pollInterval);
                continue;
            }

            if (result.Status == IndexerExecutionStatus.InProgress ||
                result.Status == IndexerExecutionStatus.Reset)
            {
                if (deadline.HasValue && DateTimeOffset.UtcNow >= deadline.Value)
                {
                    logger.LogWarning(
                        "WaitForIndexerCompletion: max wait of {MaxWaitDuration} exceeded with indexer still reporting {Status}; returning stale status to caller.",
                        maxWaitDuration,
                        result.Status);
                    return status;
                }

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
                var matchesByStart = candidate.StartTime.HasValue && candidate.StartTime.Value >= minRunStartUtc.Value;
                var matchesByEnd = candidate.EndTime.HasValue && candidate.EndTime.Value >= minRunStartUtc.Value;
                if (!matchesByStart && !matchesByEnd)
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

        // IMAGE PROJECTION — this SQL is the pull-path mirror of the push-path C# helper
        // SearchEpisodeImage (RedditPodcastPoster.EntitySearchIndexer), the single documented source
        // of truth. Cosmos SQL cannot call C#, so the two are kept in sync. The rules:
        //   1. image = youtube ?? spotify ?? apple ?? other  (first available, YouTube-first).
        //   2. LOSS-LESS COMPACTION: the winner is stored as a short token the client expands back
        //      to the EXACT same URL (never a coarse quality guess):
        //        - YouTube "y{q}": a standard i.ytimg.com/vi/{youTubeId}/{quality}.jpg thumbnail for
        //          this episode's video (the placeholder-safe image chosen at ingestion). The video
        //          id is dropped (== youtubeId) and the quality is one char mapped from the filename
        //          AS-IS: x=maxresdefault, s=sddefault, h=hqdefault, m=mqdefault, d=default.
        //        - Spotify "s{id}": a standard i.scdn.co/image/{id} cover (single segment, no
        //          query/fragment) -> drop the fixed prefix.
        //        - Apple "a{n}{path}": a standard is{n}-ssl.mzstatic.com/image/thumb/{path} artwork
        //          -> drop the fixed prefix, keep host digit {n} and the deep {path} verbatim.
        //      Any other shape (other art, non-standard URL, thumbnail for a different video, a URL
        //      with a query) is kept as its FULL URL in image — nothing lossy is dropped.
        //   3. image is always non-null. When there is no image it is an EMPTY STRING, never null:
        //      Azure AI Search merge ignores null source values, so an empty string (or a real
        //      value) is required to clear/overwrite a previously-indexed image during incremental
        //      (high-water-mark) reindexing (e.g. a stale Spotify cover before YouTube).
        //   4. youtubeImageVariant has been removed entirely (field no longer exists on the index):
        //      a full reindex / index recreate is required after deploy so every document carries a
        //      lossless `image` token/URL and clients no longer rely on the coarse variant.
        const string spotifyPrefix = "https://i.scdn.co/image/";
        const string appleHostTail = "-ssl.mzstatic.com/image/thumb/";
        // Full Apple prefix = "https://is" (10) + digit (1) + appleHostTail => digit at index 10.
        var applePrefixLength = "https://is".Length + 1 + appleHostTail.Length;
        var isYouTubeToken =
            @$"(IS_DEFINED(e.images.youtube) AND IS_DEFINED(e.youTubeId)
                AND STARTSWITH(e.images.youtube, CONCAT(""https://i.ytimg.com/vi/"", e.youTubeId, ""/""))
                AND (ENDSWITH(e.images.youtube, ""/maxresdefault.jpg"")
                    OR ENDSWITH(e.images.youtube, ""/sddefault.jpg"")
                    OR ENDSWITH(e.images.youtube, ""/hqdefault.jpg"")
                    OR ENDSWITH(e.images.youtube, ""/mqdefault.jpg"")
                    OR ENDSWITH(e.images.youtube, ""/default.jpg"")))";
        var youTubeToken =
            @"CONCAT(""y"",
                IIF(ENDSWITH(e.images.youtube, ""/maxresdefault.jpg""), ""x"",
                    IIF(ENDSWITH(e.images.youtube, ""/sddefault.jpg""), ""s"",
                        IIF(ENDSWITH(e.images.youtube, ""/hqdefault.jpg""), ""h"",
                            IIF(ENDSWITH(e.images.youtube, ""/mqdefault.jpg""), ""m"", ""d"")))))";
        var isSpotifyToken =
            @$"((NOT IS_DEFINED(e.images.youtube)) AND IS_DEFINED(e.images.spotify)
                AND STARTSWITH(e.images.spotify, ""{spotifyPrefix}"")
                AND LENGTH(e.images.spotify) > {spotifyPrefix.Length}
                AND (NOT CONTAINS(SUBSTRING(e.images.spotify, {spotifyPrefix.Length}, LENGTH(e.images.spotify) - {spotifyPrefix.Length}), ""/""))
                AND (NOT CONTAINS(e.images.spotify, ""?""))
                AND (NOT CONTAINS(e.images.spotify, ""#"")))";
        var spotifyToken =
            @$"CONCAT(""s"", SUBSTRING(e.images.spotify, {spotifyPrefix.Length}, LENGTH(e.images.spotify) - {spotifyPrefix.Length}))";
        var isAppleToken =
            @$"((NOT IS_DEFINED(e.images.youtube)) AND (NOT IS_DEFINED(e.images.spotify)) AND IS_DEFINED(e.images.apple)
                AND (STARTSWITH(e.images.apple, ""https://is1{appleHostTail}"")
                    OR STARTSWITH(e.images.apple, ""https://is2{appleHostTail}"")
                    OR STARTSWITH(e.images.apple, ""https://is3{appleHostTail}"")
                    OR STARTSWITH(e.images.apple, ""https://is4{appleHostTail}"")
                    OR STARTSWITH(e.images.apple, ""https://is5{appleHostTail}"")))";
        var appleToken =
            @$"CONCAT(""a"", SUBSTRING(e.images.apple, 10, 1), SUBSTRING(e.images.apple, {applePrefixLength}, LENGTH(e.images.apple) - {applePrefixLength}))";
        var query = @$"SELECT
                            e.id,
                            e.title as episodeTitle,
                            e.podcastName as podcastName,
                            IIF(LENGTH(e.description) > {Constants.DescriptionSize},
                                CONCAT(SUBSTRING(e.description, 0, {Constants.DescriptionSize - 1}), ""{"\u2026"}""),
                                e.description) as episodeDescription,
                            e.release,
                            IIF(ENDSWITH(e.duration, "".0000000""), SUBSTRING(e.duration, 0, LENGTH(e.duration) - 8), e.duration) as duration,
                            IIF(IS_DEFINED(e.spotifyId) AND e.spotifyId != """", e.spotifyId, null) as spotifyId,
                            IIF(IS_DEFINED(e.appleId), ToString(e.appleId), null) as appleId,
                            IIF(IS_DEFINED(e.urls.apple) AND CONTAINS(e.urls.apple, ""/id"") AND CONTAINS(e.urls.apple, ""?i=""),
                                SUBSTRING(e.urls.apple,
                                    INDEX_OF(e.urls.apple, ""/id"") + 3,
                                    INDEX_OF(e.urls.apple, ""?i="") - INDEX_OF(e.urls.apple, ""/id"") - 3),
                                null) as podcastAppleId,
                            IIF(IS_DEFINED(e.youTubeId) AND e.youTubeId != """", e.youTubeId, null) as youtubeId,
                            e.urls.bbc,
                            e.urls.internetArchive,
                            e.subjects as subjects,
                            e.podcastSearchTerms as podcastSearchTerms,
                            e.searchTerms as episodeSearchTerms,
                            IIF({isYouTubeToken}, {youTubeToken},
                                IIF({isSpotifyToken}, {spotifyToken},
                                    IIF({isAppleToken}, {appleToken},
                                        (e.images.youtube ?? e.images.spotify ?? e.images.apple ?? e.images.other) ?? """"))) as image,
                            e.lang ?? e.podcastLanguage as lang,
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
            result = await searchIndexerClient.DeleteIndexerAsync(request.IndexerName, CancellationToken.None);
            if (result.Status != (int)HttpStatusCode.NoContent && result.Status != (int)HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Unable to tear-down indexer '{request.IndexerName}': HTTP {result.Status} {result.ReasonPhrase}.");
            }

            logger.LogInformation("Tear-down indexer '{IndexerName}': HTTP {Status}.", request.IndexerName, result.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.IndexName))
        {
            result = await searchIndexClient.DeleteIndexAsync(request.IndexName, CancellationToken.None);
            if (result.Status != (int)HttpStatusCode.NoContent && result.Status != (int)HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Unable to tear-down index '{request.IndexName}': HTTP {result.Status} {result.ReasonPhrase}.");
            }

            logger.LogInformation("Tear-down index '{IndexName}': HTTP {Status}.", request.IndexName, result.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.DataSourceName))
        {
            result = await searchIndexerClient.DeleteDataSourceConnectionAsync(request.DataSourceName, CancellationToken.None);
            if (result.Status != (int)HttpStatusCode.NoContent && result.Status != (int)HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Unable to tear-down data-source '{request.DataSourceName}': HTTP {result.Status} {result.ReasonPhrase}.");
            }

            logger.LogInformation("Tear-down data-source '{DataSourceName}': HTTP {Status}.", request.DataSourceName, result.Status);
        }
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

    private async Task<bool> LogPotentialDuplicatePair()
    {
        using var cosmosClient = CreateCosmosClient();
        var container = cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.EpisodesContainer);

        var query = $@"SELECT e.id, e.podcastId, e.title, e.release, e.spotifyId, e.appleId, e.youTubeId, e.podcastName
                       FROM episodes e
                       WHERE {ActiveEpisodesFilter}";
        var iterator = container.GetItemQueryIterator<EpisodeDuplicateSample>(new QueryDefinition(query));
        var seen = new Dictionary<string, EpisodeDuplicateSample>(StringComparer.Ordinal);
        var hasDuplicates = false;

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
                    if (string.Equals(existing.PodcastId, item.PodcastId, StringComparison.OrdinalIgnoreCase))
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
                        hasDuplicates = true;
                    }
                }

                seen[fingerprint] = item;
            }
        }

        if (!hasDuplicates)
        {
            logger.LogInformation("No potential duplicate episode pair was found in Cosmos for the current filter.");
        }
        else
        {
            logger.LogWarning("Potential duplicate episode pairs found. Run FindDuplicateEpisodes for detailed field comparison.");
        }

        return hasDuplicates;
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