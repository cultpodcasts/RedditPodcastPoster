using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public interface IPostResolver
{
    IEnumerable<Post> FindEpisodePosts(PodcastEpisodeV2 podcastEpisode);
}