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
        logger.LogInformation("There are '{Count}' episodes matching date", matchingEpisodes.Count());
        foreach (var episode in matchingEpisodes)
        {
            if (episode.AppleId.HasValue)
            {
                var appleEpisode = appleEpisodes.SingleOrDefault(x => x.AppleId == episode.AppleId.Value);
                if (appleEpisode != null)
                {
                    logger.LogInformation("Updating '{EpisodeTitle}' to '{AppleEpisodeRelease:G}'.", episode.Title, appleEpisode.Release);
                    episode.Release = appleEpisode.Release;
                    updated = true;
                }
                else
                {
                    logger.LogWarning("'{EpisodeTitle}' has no matching episode'.", episode.Title);
                }
            }
            else
            {
                logger.LogWarning("'{EpisodeTitle}' has no apple-id'.", episode.Title);

            }
        }

        if (updated)
        {
            await podcastRepository.Save(podcast);
        }
    }
}