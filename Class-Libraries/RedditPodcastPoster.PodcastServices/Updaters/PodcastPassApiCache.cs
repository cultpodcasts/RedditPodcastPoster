using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;

namespace RedditPodcastPoster.PodcastServices.Updaters;

public class PodcastPassApiCache(
    IEnumerable<IPodcastPassApiCacheSource> sources,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastPassApiCache> logger)
#pragma warning restore CS9113 // Parameter is unread.
    : IPodcastPassApiCache
{
    public void Clear()
    {
        foreach (var source in sources)
        {
            source.ClearPassCache();
        }
    }
}
