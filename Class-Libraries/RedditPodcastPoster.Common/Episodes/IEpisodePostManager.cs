using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodePostManager
{
    Task<ProcessResponse> Post(PostModel postModel);
}