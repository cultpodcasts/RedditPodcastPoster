using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class PostManager(
    IPostResolver postResolver,
    IFlareManager flareManager,
    ILogger<PostManager> logger
) : IPostManager
{
    public async Task RemoveEpisodePost(PodcastEpisode podcastEpisode)
    {
        var subredditEpisodePosts = postResolver.FindEpisodePosts(podcastEpisode);
        foreach (var subredditEpisodePost in subredditEpisodePosts)
        {
            logger.LogInformation("Removing post '{Title}'.", subredditEpisodePost.Title);
            await subredditEpisodePost.DeleteAsync();
        }
    }

    public async Task UpdateFlare(PodcastEpisode podcastEpisode)
    {
        var subredditEpisodePosts = postResolver.FindEpisodePosts(podcastEpisode);
        foreach (var subredditEpisodePost in subredditEpisodePosts)
        {
            await flareManager.SetFlare(podcastEpisode.Episode.Subjects.ToArray(), subredditEpisodePost);
        }
    }
}