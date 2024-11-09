using Reddit.Controllers;

namespace RedditPodcastPoster.Reddit;

public interface IFlareManager
{
    public Task<FlareState> SetFlare(string[] subjectNames, Post post);
}