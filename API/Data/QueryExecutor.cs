using System.Text.RegularExpressions;
using API.Dtos;
using API.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Text;

namespace API.Data;

public class QueryExecutor : IQueryExecutor
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _cosmosDbSettings;
    private readonly ITextSanitiser _textSanitiser;

    public QueryExecutor(
        CosmosClient cosmosClient,
        ITextSanitiser textSanitiser,
        IOptions<CosmosDbSettings> cosmosDbSettings)
    {
        _cosmosClient = cosmosClient;
        _textSanitiser = textSanitiser;
        _cosmosDbSettings = cosmosDbSettings.Value;
    }

    public async Task<HomePageModel> GetHomePage(CancellationToken ct)
    {
        var podcastResults = new List<PodcastResult>();
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);

        var lastWeeksEpisodes = new QueryDefinition(
            @"
                            SELECT
                            p.name as podcastName,
                            p.titleRegex as titleRegex,
                            p.descriptionRegex as descriptionRegex,
                            e.title as episodeTitle,
                            e.description as episodeDescription,
                            e.release as release,
                            e.urls.spotify as spotify,
                            e.urls.apple as apple,
                            e.urls.youtube as youtube
                            FROM
                            podcasts p
                            JOIN
                            e IN p.episodes
                            WHERE DateTimeDiff('dd', e.release, GetCurrentDateTime()) < 7"
        );

        using var feed = c.GetItemQueryIterator<PodcastResult>(lastWeeksEpisodes);
        while (feed.HasMoreResults)
        {
            podcastResults.AddRange(await feed.ReadNextAsync(ct));
        }

        var numberOfEpisodes = new QueryDefinition(@"
                SELECT Count(p.episodes)
                FROM
                  podcasts p
                JOIN
                e IN p.episodes
                WHERE e.ignored=false AND e.removed=false
                ");
        using var episodeCount = c.GetItemQueryIterator<ScalarResult<int>>(
            numberOfEpisodes
        );
        int? count = null;
        if (episodeCount.HasMoreResults)
        {
            var item = await episodeCount.ReadNextAsync(ct);
            count = item.First().item;
        }

        return new HomePageModel
        {
            EpisodeCount = count,
            RecentEpisodes = podcastResults.OrderByDescending(x => x.Release).Select(Santitise)
        };
    }

    private PodcastResult Santitise(PodcastResult podcastResult)
    {
        if (!string.IsNullOrWhiteSpace(podcastResult.TitleRegex))
        {
            var titleRegex = new Regex(podcastResult.TitleRegex);
            podcastResult.EpisodeTitle = _textSanitiser.ExtractTitle(podcastResult.EpisodeTitle, titleRegex);
        }

        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            var descRegex = new Regex(podcastResult.DescriptionRegex);
            podcastResult.EpisodeDescription = _textSanitiser.ExtractBody(podcastResult.EpisodeDescription, descRegex);
        }

        return podcastResult;
    }
}