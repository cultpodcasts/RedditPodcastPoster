using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class IndexablePodcastIdProvider(
    IPodcastRepository podcastRepository,
    ILogger<IndexablePodcastIdProvider> logger
) : IIndexablePodcastIdProvider
{
    public IAsyncEnumerable<Guid> GetIndexablePodcastIds()
    {
        logger.LogInformation($"{nameof(GetIndexablePodcastIds)} Retrieving podcasts.");
        var podcastIds = podcastRepository.GetAllBy(
            podcast => ((!podcast.Removed.IsDefined() || podcast.Removed == false) &&
                        podcast.IndexAllEpisodes) ||
                       !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex),
            x => x.Id);
        return podcastIds;
    }
}