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
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts)
        {
            foreach (var episode in podcast.Episodes)
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