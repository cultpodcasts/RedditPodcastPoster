using RedditPodcastPoster.SocialPosting.Models;
using RedditPodcastPoster.Models.Posting;

namespace RedditPodcastPoster.SocialPosting.Episodes;

public interface IEpisodePostManager
{
    Task<ProcessResponse> Post(PostModel postModel);
}