using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
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



        var container = "redditposts";
        //        var podcasts = await _cosmosDbRepository.GetAll<Podcast>().ToListAsync();
        string after = string.Empty;
        var redditPosts = _redditClient.Subreddit(_subredditSettings.SubredditName).Posts
            .GetNew(after: after, limit: 10).ToList();
        while (redditPosts.Any())
        {
            foreach (var post in redditPosts)
            {
                await _fileRepository.Write(post.Fullname, post);
            }
            after = redditPosts.Last().Fullname;
            redditPosts = _redditClient.Subreddit(_subredditSettings.SubredditName).Posts
                .GetNew(limit: 10, after: after);
        }


    }
}