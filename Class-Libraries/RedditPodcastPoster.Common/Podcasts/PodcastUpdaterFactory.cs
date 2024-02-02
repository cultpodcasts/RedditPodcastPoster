using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastUpdaterFactory(IServiceProvider serviceProvider, ILogger<PodcastUpdaterFactory> logger): IPodcastUpdaterFactory
{
    public IPodcastUpdater Create()
    {
        var podcastUpdater = serviceProvider.GetService<IPodcastUpdater>();
        if (podcastUpdater == null)
        {
            throw new InvalidOperationException($"Unable to create '{nameof(IPodcastUpdater)}'.");
        }
        return podcastUpdater!;
    }
}