using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.Common.Episodes;

public interface IEpisodePostManager
{
    Task<ProcessResponse> Post(PostModel postModel);
}