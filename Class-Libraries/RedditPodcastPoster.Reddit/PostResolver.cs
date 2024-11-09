using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class PostResolver(
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PostResolver> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPostResolver
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public IEnumerable<Post> FindEpisodePosts(PodcastEpisode podcastEpisode)
    {
        var subredditPosts = redditClient.Subreddit(_subredditSettings.SubredditName).Posts.New;
        var subredditEpisodePosts = subredditPosts
            .OfType<LinkPost>()
            .Where(x =>
                // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                x != null &&
                // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                x.Author == redditClient.Account.Me.Name &&
                ((podcastEpisode.Episode.Urls.Apple != null && x.URL == podcastEpisode.Episode.Urls.Apple.ToString()) ||
                 (podcastEpisode.Episode.Urls.Spotify != null &&
                  x.URL == podcastEpisode.Episode.Urls.Spotify.ToString()) ||
                 (podcastEpisode.Episode.Urls.YouTube != null &&
                  x.URL == podcastEpisode.Episode.Urls.YouTube.ToString())));
        return subredditEpisodePosts;
    }
}