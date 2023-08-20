namespace RedditPodcastPoster.Common;

public interface IPodcastProcessor
{
    Task<ProcessResponse> Process(ProcessRequest processRequest);
}