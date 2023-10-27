using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.ContentPublisher;

public class QueryExecutor : IQueryExecutor
{
    private readonly CosmosClient _cosmosClient;
    private readonly CosmosDbSettings _cosmosDbSettings;
    private readonly ILogger<QueryExecutor> _logger;
    private readonly ITextSanitiser _textSanitiser;

    public QueryExecutor(
        CosmosClient cosmosClient,
        ITextSanitiser textSanitiser,
        IOptions<CosmosDbSettings> cosmosDbSettings,
        ILogger<QueryExecutor> logger)
    {
        _cosmosClient = cosmosClient;
        _textSanitiser = textSanitiser;
        _logger = logger;
        _cosmosDbSettings = cosmosDbSettings.Value;
    }

    public async Task<HomePageModel> GetHomePage(CancellationToken ct)
    {
        var c = _cosmosClient.GetContainer(_cosmosDbSettings.DatabaseId, _cosmosDbSettings.Container);

        var podcastResults = GetRecentPodcasts(ct, c);

        var count = GetEpisodeCount(ct, c);

        var totalDuration = GetTotalDuration(ct, c);

        IEnumerable<Task> tasks = new Task[] {podcastResults, count, totalDuration};

        await Task.WhenAll(tasks);

        return new HomePageModel
        {
            EpisodeCount = count.Result,
            RecentEpisodes = podcastResults.Result.OrderByDescending(x => x.Release).Select(Santitise).Select(x =>
                new RecentEpisode
                {
                    Apple = x.Apple,
                    EpisodeDescription = x.EpisodeDescription,
                    EpisodeTitle = x.EpisodeTitle,
                    PodcastName = x.PodcastName,
                    Release = x.Release,
                    Spotify = x.Spotify,
                    YouTube = x.YouTube,
                    Length = TimeSpan.FromSeconds(Math.Round((double) x.Length.TotalSeconds))
                }),
            TotalDuration = totalDuration.Result
        };
    }

    private static async Task<int?> GetEpisodeCount(CancellationToken ct, Container c)
    {
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
            count = item.First().Item;
        }

        return count;
    }

    private static async Task<TimeSpan> GetTotalDuration(CancellationToken ct, Container c)
    {
        var durationResults = new List<DurationResult>();

        var allDurations = new QueryDefinition(@"
               SELECT e.duration
               FROM
                 podcasts p
               JOIN
               e IN p.episodes
               WHERE e.ignored=false AND e.removed=false
        ");
        using var feed = c.GetItemQueryIterator<DurationResult>(allDurations);
        while (feed.HasMoreResults)
        {
            durationResults.AddRange(await feed.ReadNextAsync(ct));
        }

        return TimeSpan.FromTicks(durationResults.Sum(x => x.Duration.Ticks));
    }

    private static async Task<List<PodcastResult>> GetRecentPodcasts(CancellationToken ct, Container c)
    {
        var podcastResults = new List<PodcastResult>();
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
                           e.urls.youtube as youtube,
                           e.duration as length
                           FROM
                           podcasts p
                           JOIN
                           e IN p.episodes
                           WHERE e.removed=false
                           AND e.ignored=false
                           AND DateTimeDiff('dd', e.release, GetCurrentDateTime()) < 7
            ");

        using var feed = c.GetItemQueryIterator<PodcastResult>(lastWeeksEpisodes);
        while (feed.HasMoreResults)
        {
            var readNextAsync = await feed.ReadNextAsync(ct);
            podcastResults.AddRange(readNextAsync);
        }

        return podcastResults;
    }

    private PodcastResult Santitise(PodcastResult podcastResult)
    {
        Regex? titleRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.TitleRegex))
        {
            titleRegex = new Regex(podcastResult.TitleRegex);
        }

        podcastResult.EpisodeTitle = _textSanitiser.SanitiseTitle(podcastResult.EpisodeTitle, titleRegex);

        Regex? descRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            descRegex = new Regex(podcastResult.DescriptionRegex);
        }

        podcastResult.EpisodeDescription =
            _textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);

        podcastResult.PodcastName = _textSanitiser.SanitisePodcastName(podcastResult.PodcastName);

        return podcastResult;
    }
}