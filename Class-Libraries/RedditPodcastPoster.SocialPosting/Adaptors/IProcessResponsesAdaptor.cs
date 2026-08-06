using RedditPodcastPoster.SocialPosting.Models;

namespace RedditPodcastPoster.SocialPosting.Adaptors;

public interface IProcessResponsesAdaptor
{
    ProcessResponse CreateResponse(IList<ProcessResponse> matchingPodcastEpisodeResults);
}