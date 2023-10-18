using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using RedditPodcastPoster.Reddit;

namespace RedditPodcastPoster.Subreddit;

public class SubredditPostProvider : ISubredditPostProvider
{
    private readonly ILogger<SubredditPostProvider> _logger;
    private readonly RedditClient _redditClient;
    private readonly SubredditSettings _subredditSettings;

    public SubredditPostProvider(
        RedditClient redditClient,
        IOptions<SubredditSettings> subredditSettings,
        ILogger<SubredditPostProvider> logger)
    {
        _redditClient = redditClient;
        _subredditSettings = subredditSettings.Value;
        _logger = logger;
    }

    public IEnumerable<Post> GetPosts()
    {
        var posts = new List<Post>();
        var after = string.Empty;
        var redditPostBatch =
            _redditClient
                .Subreddit(_subredditSettings.SubredditName).Posts
                .GetNew(after, limit: 10)
                .ToList();
        while (redditPostBatch.Any())
        {
            posts.AddRange(redditPostBatch);
            after = redditPostBatch.Last().Fullname;
            redditPostBatch = _redditClient.Subreddit(_subredditSettings.SubredditName).Posts
                .GetNew(limit: 10, after: after);
        }

        return posts;
    }
}