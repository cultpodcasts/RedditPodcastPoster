using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Extensions;

namespace EnrichPodcastWithImages;

public class Processor(
    IPodcastRepository podcastRepository,
    IImageUpdater imageUpdater,
    ILogger<Processor> logger)
{
    public async Task Run(Request request)
    {
        List<Guid> podcastIds;
        Func<Episode, bool> episodeSelector;
        if (!string.IsNullOrWhiteSpace(request.PodcastPartialMatch))
        {
            var ids =
                await podcastRepository.GetAllBy(
                        x => x.Name.ToLower().Contains(request.PodcastPartialMatch.ToLower()),
                        p => new {id = p.Id})
                    .ToListAsync();
            podcastIds = ids.Select(x => x.id).ToList();
            episodeSelector = episode => true;
        }
        else
        {
            var ids =
                await podcastRepository.GetAllBy(
                        x => x.Episodes.Any(x => x.Subjects.Contains(request.Subject)),
                        p => new {id = p.Id})
                    .ToListAsync();
            podcastIds = ids.Select(x => x.id).ToList();
            episodeSelector = episode => episode.Subjects.Contains(request.Subject);
        }

        if (!podcastIds.Any())
        {
            logger.LogError("No podcasts found for partial-name '{podcastPartialName}'.",
                request.PodcastPartialMatch);
            return;
        }

        var indexingContext = new IndexingContext();
        foreach (var podcastId in podcastIds)
        {
            var updatedEpisodes = 0;

            var podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
            if (podcast == null)
            {
                logger.LogError("No podcast with podcast-id '{podcastId}' found.", podcastId);
                continue;
            }

            logger.LogInformation("Enriching podcast '{podcastName}'.", podcast.Name);
            var episodes = podcast.Episodes.Where(episodeSelector);
            foreach (var episode in episodes)
            {
                var imageUpdateRequest = (podcast, episode).ToEpisodeImageUpdateRequest();
                var updated = await imageUpdater.UpdateImages(podcast, episode, imageUpdateRequest, indexingContext);
                if (updated)
                {
                    updatedEpisodes++;
                }
            }

            logger.LogInformation("Updated {updatedEpisodes} episodes.", updatedEpisodes);
            if (updatedEpisodes > 0)
            {
                await podcastRepository.Save(podcast);
            }
        }
    }
}