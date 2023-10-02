using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;

namespace RedditPodcastPoster.CosmosDbFixer;

public class CosmosDbFixer
{
    private readonly ILogger<CosmosDbRepository> _logger;
    private readonly IPodcastRepository _podcastRepository;

    public CosmosDbFixer(
        IPodcastRepository podcastRepository,
        ILogger<CosmosDbRepository> logger)
    {
        _podcastRepository = podcastRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts)
        {
            foreach (var episode in podcast.Episodes)
            {
                if (episode.Urls.Apple != null)
                {
                    episode.Urls.Apple = episode.Urls.Apple.CleanAppleUrl();
                }
            }

            await _podcastRepository.Save(podcast);
        }
    }
}