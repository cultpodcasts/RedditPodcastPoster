using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;

namespace FixDatesFromApple;

public class Processor(
    IPodcastRepository podcastRepository,
    IAppleEpisodeProvider appleEpisodeProvider,
    ILogger<Processor> logger
)
{
    public async Task Run(FixRequest request)
    {
        var podcast = await podcastRepository.GetPodcast(request.PodcastId);
        if (podcast == null || !podcast.AppleId.HasValue)
        {
            throw new ArgumentException("Podcast is null or has no apple-id");
        }

        var appleEpisodes = await appleEpisodeProvider.GetEpisodes(
            new ApplePodcastId(podcast.AppleId.Value),
            new IndexingContext());
        var updated = false;

        var matchingEpisodes = podcast.Episodes.Where(x => DateOnly.FromDateTime(x.Release) == request.Date);
        logger.LogInformation($"There are '{matchingEpisodes.Count()}' episodes matching date");
        foreach (var episode in matchingEpisodes)
        {
            if (episode.AppleId.HasValue)
            {
                var appleEpisode = appleEpisodes.SingleOrDefault(x => x.AppleId == episode.AppleId.Value);
                if (appleEpisode != null)
                {
                    logger.LogInformation($"Updating '{episode.Title}' to '{appleEpisode.Release:G}'.");
                    episode.Release = appleEpisode.Release;
                    updated = true;
                }
                else
                {
                    logger.LogWarning($"'{episode.Title}' has no matching episode'.");
                }
            }
            else
            {
                logger.LogWarning($"'{episode.Title}' has no apple-id'.");

            }
        }

        if (updated)
        {
            await podcastRepository.Save(podcast);
        }
    }
}