using Reddit.Controllers;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit.Resolvers;

public interface IPostResolver
{
    IEnumerable<Post> FindEpisodePosts(PodcastEpisode podcastEpisode);
}
