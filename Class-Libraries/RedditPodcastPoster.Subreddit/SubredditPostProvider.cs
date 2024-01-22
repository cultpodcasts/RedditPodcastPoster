using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using RedditPodcastPoster.Reddit;

namespace RedditPodcastPoster.Subreddit;

public class SubredditPostProvider(
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubredditPostProvider> logger)
    : ISubredditPostProvider
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public IEnumerable<Post> GetPosts()
    {
        var posts = new List<Post>();
        var after = string.Empty;
        var redditPostBatch =
            redditClient
                .Subreddit(_subredditSettings.SubredditName).Posts
                .GetNew(after, limit: 10)
                .ToList();
        while (redditPostBatch.Any())
        {
            posts.AddRange(redditPostBatch);
            after = redditPostBatch.Last().Fullname;
            redditPostBatch = redditClient.Subreddit(_subredditSettings.SubredditName).Posts
                .GetNew(limit: 10, after: after);
        }

        return posts;
    }
}