using Reddit.Controllers;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Reddit.Resolvers;

public interface IPostResolver
{
    IEnumerable<Post> FindEpisodePosts(PodcastEpisode podcastEpisode);
}
