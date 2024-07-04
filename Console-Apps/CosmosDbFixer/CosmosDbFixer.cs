using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace CosmosDbFixer;

public class CosmosDbFixer(
    IPodcastRepository podcastRepository,
    ILogger<CosmosDbRepository> logger)
{
    private readonly ILogger<CosmosDbRepository> _logger = logger;

    public async Task Run()
    {
        var podcastIds = await podcastRepository.GetAllIds().ToListAsync();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
            foreach (var episode in podcast!.Episodes)
            {
                if (episode.Urls.Apple != null)
                {
                    episode.Urls.Apple = episode.Urls.Apple.CleanAppleUrl();
                }
            }

            await podcastRepository.Save(podcast);
        }
    }
}