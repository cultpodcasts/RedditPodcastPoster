using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Extensions;

namespace EnrichPodcastWithImages;

public class Processor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IImageUpdater imageUpdater,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<Processor> logger)
{
    public async Task Run(Request request)
    {
        List<Guid> podcastIds;
        Func<Episode, bool> episodeSelector;
        if (!string.IsNullOrWhiteSpace(request.PodcastPartialMatch))
        {
            var ids = await podcastRepository.GetAllBy(
                    x => x.Name.ToLower().Contains(request.PodcastPartialMatch.ToLower()))
                .ToListAsync();
            podcastIds = ids.Select(x => x.Id).ToList();
            episodeSelector = _ => true;
        }
        else
        {
            var ids = new HashSet<Guid>();
            await foreach (var episode in episodeRepository.GetAllBy(x => x.Subjects.Contains(request.Subject)))
            {
                ids.Add(episode.PodcastId);
            }

            podcastIds = ids.ToList();
            episodeSelector = episode => episode.Subjects.Contains(request.Subject);
        }

        if (!podcastIds.Any())
        {
            logger.LogError("No podcasts found for partial-name '{podcastPartialName}'.",
                request.PodcastPartialMatch);
            return;
        }

        var indexingContext = new IndexingContext();
        var updatedEpisodeIds = new List<Guid>();
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
            var episodes = await episodeRepository.GetByPodcastId(podcastId)
                .Where(episodeSelector)
                .ToListAsync();

            foreach (var episode in episodes)
            {
                var imageUpdateRequest = (podcast, episode).ToEpisodeImageUpdateRequest();
                var updated = await imageUpdater.UpdateImages(podcast, episode, imageUpdateRequest, indexingContext);
                if (updated)
                {
                    await episodeRepository.Save(episode);
                    updatedEpisodeIds.Add(episode.Id);
                    updatedEpisodes++;
                }
            }

            logger.LogInformation("Updated {updatedEpisodes} episodes.", updatedEpisodes);
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}