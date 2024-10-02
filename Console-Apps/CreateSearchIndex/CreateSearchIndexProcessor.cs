using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Search;

namespace CreateSearchIndex;

public partial class CreateSearchIndexProcessor(
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    ISearchIndexerService searchIndexerService,
    IOptions<CosmosDbSettings> cosmosDbSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CreateSearchIndexProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private static readonly Regex Whitespace = CreateWhitespaceRegex();
    private static readonly TimeSpan IndexAtMinutes = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan Frequency = TimeSpan.FromMinutes(30);
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

    public async Task Process(CreateSearchIndexRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.IndexName))
        {
            if (request.TearDownIndex)
            {
                await searchIndexClient.DeleteIndexAsync(request.IndexName);
                logger.LogWarning(
                    "Ensure that the indexer has run and completed indexing ALL records. The indexer will time-out at 10,000 records, so it must be re-run until all records are re-indexed.");
            }

            var index = new SearchIndex(request.IndexName)
            {
//                Fields = [],
                Fields = new FieldBuilder
                {
                    Serializer = new JsonObjectSerializer(new JsonSerializerOptions
                        {PropertyNamingPolicy = JsonNamingPolicy.CamelCase})
                }.Build(typeof(EpisodeSearchRecord)),
                DefaultScoringProfile = string.Empty,
                CorsOptions = new CorsOptions(["*"]) {MaxAgeInSeconds = 300}
            };
            var searchIndexCreateResponse = await searchIndexClient.CreateOrUpdateIndexAsync(index);
        }

        if (!string.IsNullOrWhiteSpace(request.DataSourceName))
        {
            var connectionString =
                $"AccountEndpoint={_cosmosDbSettings.Endpoint};Database={_cosmosDbSettings.DatabaseId};AccountKey={_cosmosDbSettings.AuthKeyOrResourceToken}";
            var query = @"SELECT
                            e.id,
                            e.title as episodeTitle,
                            p.name as podcastName,
                            e.description as episodeDescription,
                            e.release,
                            e.duration,
                            e.explicit,
                            e.urls.spotify,
                            e.urls.apple,
                            e.urls.youtube,
                            e.subjects as subjects,
                            p.searchTerms as podcastSearchTerms,
                            e.searchTerms as episodeSearchTerms,
                            p._ts
                            FROM podcasts p
                            JOIN e IN p.episodes
                            WHERE ((NOT IS_DEFINED(p.removed)) OR p.removed=false)
                              and e.removed = false 
                              and p._ts >= @HighWaterMark
                            ORDER BY p._ts";
            query = Whitespace.Replace(query, " ").Trim();
            var searchIndexDataContainer = new SearchIndexerDataContainer(_cosmosDbSettings.Container)
            {
                Query = query
            };
            var searchIndexDataSourceConnection = new SearchIndexerDataSourceConnection(request.DataSourceName,
                SearchIndexerDataSourceType.CosmosDb, connectionString, searchIndexDataContainer)
            {
                DataChangeDetectionPolicy = new HighWaterMarkChangeDetectionPolicy("_ts")
            };

            var dataSource =
                await searchIndexerClient.CreateOrUpdateDataSourceConnectionAsync(searchIndexDataSourceConnection);
        }

        if (!string.IsNullOrWhiteSpace(request.IndexerName) &&
            !string.IsNullOrWhiteSpace(request.DataSourceName) &&
            !string.IsNullOrWhiteSpace(request.IndexName))
        {
            var nextIndex = DateTimeOffset.Now
                .Add(Frequency)
                .Floor(Frequency)
                .Add(IndexAtMinutes);

            var indexingSchedule = new IndexingSchedule(Frequency) {StartTime = nextIndex};
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
                        {"assumeOrderByHighWaterMarkColumn", true}
                    }
                }
            };
            await searchIndexerClient.CreateOrUpdateIndexerAsync(searchIndexer);
        }

        if (request.RunIndexer)
        {
            await searchIndexerService.RunIndexer();
        }
    }

    [GeneratedRegex(@"\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex CreateWhitespaceRegex();
}