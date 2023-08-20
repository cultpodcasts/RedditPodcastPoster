using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices;

public interface IServiceResolver
{
    Task<Uri?> Resolve(Podcast podcast, Episode episode);
}