using Reddit.Controllers;
using RedditPodcastPoster.Reddit.Models;

namespace RedditPodcastPoster.Reddit.Managers;

public interface IFlareManager
{
    public Task<FlareState> SetFlare(string[] subjectNames, Post post);
}