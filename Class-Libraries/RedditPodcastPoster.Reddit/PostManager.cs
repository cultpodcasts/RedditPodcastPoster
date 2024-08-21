using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit;
using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class PostManager(
    RedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<PostManager> logger
) : IPostManager
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task RemoveEpisodePost(PodcastEpisode podcastEpisode)
    {
        var subredditPosts = redditClient.Subreddit(_subredditSettings.SubredditName).Posts.New;
        var subredditEpisodePosts = subredditPosts.Cast<LinkPost>().Where(x =>
            x != null && x.Author == redditClient.Account.Me.Name &&
            ((podcastEpisode.Episode.Urls.Apple != null && x.URL == podcastEpisode.Episode.Urls.Apple.ToString()) ||
             (podcastEpisode.Episode.Urls.Spotify != null && x.URL == podcastEpisode.Episode.Urls.Spotify.ToString()) ||
             (podcastEpisode.Episode.Urls.YouTube != null && x.URL == podcastEpisode.Episode.Urls.YouTube.ToString())));
        if (subredditEpisodePosts.Any())
        {
            foreach (var subredditEpisodePost in subredditEpisodePosts)
            {
                logger.LogInformation($"Removing post '{subredditEpisodePost.Title}'.");
                await subredditEpisodePost.DeleteAsync();
            }
        }
    }
}