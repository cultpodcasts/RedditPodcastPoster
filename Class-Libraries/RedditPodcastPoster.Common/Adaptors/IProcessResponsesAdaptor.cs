using RedditPodcastPoster.Common.Models;

namespace RedditPodcastPoster.Common.Adaptors;

public interface IProcessResponsesAdaptor
{
    ProcessResponse CreateResponse(IList<ProcessResponse> matchingPodcastEpisodeResults);
}