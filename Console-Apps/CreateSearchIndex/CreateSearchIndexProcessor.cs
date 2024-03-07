using System.Text.RegularExpressions;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence;

namespace CreateSearchIndex;

public partial class CreateSearchIndexProcessor(
    SearchIndexClient searchIndexClient,
    SearchIndexerClient searchIndexerClient,
    IOptions<CosmosDbSettings> cosmosDbSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CreateSearchIndexProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private static readonly Regex Whitespace = CreateWhitespaceRegex();
    private readonly CosmosDbSettings _cosmosDbSettings = cosmosDbSettings.Value;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Used once.")]
    public async Task Process(CreateSearchIndexRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.IndexName))
        {
            if (request.TearDownIndex)
            {
                await searchIndexClient.DeleteIndexAsync(request.IndexName);
            }

            var index = new SearchIndex(request.IndexName)
            {
                Fields = new List<SearchField>
                {
                    new("id", SearchFieldDataType.String)
                    {
                        IsKey = true, IsSearchable = false, IsFilterable = false, IsSortable = false,
                        IsFacetable = false
                    },
                    new("episodeTitle", SearchFieldDataType.String)
                    {
                        IsSearchable = true, AnalyzerName = LexicalAnalyzerName.EnLucene, IsFilterable = false,
                        IsFacetable = false, IsSortable = false
                    },
                    new("podcastName", SearchFieldDataType.String)
                    {
                        IsSearchable = true, IsFilterable = true, IsFacetable = true,
                        AnalyzerName = LexicalAnalyzerName.EnLucene, IsSortable = false
                    },
                    new("episodeDescription", SearchFieldDataType.String)
                    {
                        IsSearchable = true, AnalyzerName = LexicalAnalyzerName.EnLucene, IsFilterable = false,
                        IsSortable = false, IsFacetable = false
                    },
                    new("release", SearchFieldDataType.DateTimeOffset)
                    {
                        IsSortable = true, IsFacetable = false, IsFilterable = false
                    },
                    new("duration", SearchFieldDataType.String)
                    {
                        IsSortable = true, IsSearchable = false, IsFacetable = false, IsFilterable = false
                    },
                    new("explicit", SearchFieldDataType.Boolean)
                    {
                        IsFilterable = true, IsSortable = false, IsFacetable = false
                    },
                    new("spotify", SearchFieldDataType.String)
                    {
                        IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false
                    },
                    new("apple", SearchFieldDataType.String)
                    {
                        IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false
                    },
                    new("youtube", SearchFieldDataType.String)
                    {
                        IsSearchable = false, IsSortable = false, IsFilterable = false, IsFacetable = false
                    },
                    new("subjects", SearchFieldDataType.Collection(SearchFieldDataType.String))
                    {
                        IsSearchable = true, IsFilterable = true, IsFacetable = true,
                        AnalyzerName = LexicalAnalyzerName.EnLucene
                    },
                    new("podcastSearchTerms", SearchFieldDataType.String)
                    {
                        IsSearchable = true, AnalyzerName = LexicalAnalyzerName.EnLucene, IsFilterable = false,
                        IsFacetable = false, IsSortable = false, IsHidden = true
                    },
                    new("episodeSearchTerms", SearchFieldDataType.String)
                    {
                        IsSearchable = true, AnalyzerName = LexicalAnalyzerName.EnLucene, IsFilterable = false,
                        IsFacetable = false, IsSortable = false, IsHidden = true
                    }
                },
                DefaultScoringProfile = string.Empty,
                CorsOptions = new CorsOptions(new[] {"*"}) {MaxAgeInSeconds = 300}
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
                            WHERE e.removed = false 
                              and e.ignored=false
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
            var searchIndexer = new SearchIndexer(request.IndexerName, request.DataSourceName, request.IndexName)
            {
                Schedule = new IndexingSchedule(TimeSpan.FromHours(1)),
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
    }

    [GeneratedRegex(@"\s+", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex CreateWhitespaceRegex();
}