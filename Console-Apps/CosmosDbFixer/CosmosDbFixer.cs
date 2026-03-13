using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace CosmosDbFixer;

public class CosmosDbFixer(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<CosmosDbRepository> logger)
{
    private readonly ILogger<CosmosDbRepository> _logger = logger;

    public async Task Run()
    {
        var podcastIds = await podcastRepository.GetAll().Select(x => x.Id).ToListAsync();
        foreach (var podcastId in podcastIds)
        {
            await foreach (var episode in episodeRepository.GetByPodcastId(podcastId))
            {
                if (episode.Urls.Apple != null)
                {
                    var cleaned = episode.Urls.Apple.CleanAppleUrl();
                    if (cleaned != episode.Urls.Apple)
                    {
                        episode.Urls.Apple = cleaned;
                        await episodeRepository.Save(episode);
                    }
                }
            }
        }
    }
}