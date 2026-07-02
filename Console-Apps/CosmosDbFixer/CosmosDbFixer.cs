using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using IPodcastRepository = RedditPodcastPoster.Persistence.Abstractions.IPodcastRepository;

namespace CosmosDbFixer;

public class CosmosDbFixer(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository)
{
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
