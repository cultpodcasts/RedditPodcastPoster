using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Reddit;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.EnrichEpisodesFromPostFlare;

public class SubredditPostFlareEnricher
{
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<CosmosDbRepository> _logger;
    private readonly RedditClient _redditClient;
    private readonly SubredditSettings _subredditSettings;

    public SubredditPostFlareEnricher(
        IFileRepository fileRepository,
        ICosmosDbRepository cosmosDbRepository,
        RedditClient redditClient,
        IOptions<SubredditSettings> subredditSettings,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _cosmosDbRepository = cosmosDbRepository;
        _redditClient = redditClient;
        _subredditSettings = subredditSettings.Value;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _cosmosDbRepository.GetAll<Podcast>().ToListAsync();
        var redditPosts = _redditClient.Subreddit(_subredditSettings.SubredditName).Posts.GetNew(limit:3000, before: "2023-08-11T21:35:59+00:00");
    }
}