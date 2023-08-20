using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Persistence;

public interface IKeySelector
{
    string GetKey(Podcast podcast);
}