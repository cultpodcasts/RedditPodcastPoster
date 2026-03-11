using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class IndexablePodcastIdProvider(
    IPodcastRepositoryV2 podcastRepository,
    ILogger<IndexablePodcastIdProvider> logger
) : IIndexablePodcastIdProvider
{
    public IAsyncEnumerable<Guid> GetIndexablePodcastIds()
    {
        logger.LogInformation($"{nameof(GetIndexablePodcastIds)} Retrieving podcasts.");

        //var podcastIds = podcastRepository.GetAllBy(
        //    podcast => ((!podcast.Removed.IsDefined() || podcast.Removed == false) &&
        //                podcast.IndexAllEpisodes) ||
        //               podcast.EpisodeIncludeTitleRegex != "",
        //    x => x.Id);

        var podcastIds = podcastRepository
            .GetAllBy(podcast =>
                ((!podcast.Removed.HasValue || podcast.Removed == false) && podcast.IndexAllEpisodes) ||
                podcast.EpisodeIncludeTitleRegex != string.Empty)
            .Select(x => x.Id);

        return podcastIds;
    }
}