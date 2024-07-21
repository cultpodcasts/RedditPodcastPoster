using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlShortening;

namespace KVWriter;

public class KVWriterProcessor(
    IPodcastRepository podcastRepository,
    IShortnerService shortnerService,
    ILogger<KVWriterProcessor> logger
)
{
    public async Task Process(KVWriterRequest request)
    {
        if (request.ItemsToTake != null)
        {
            logger.LogInformation("Getting podcasts");
            var podcasts = await podcastRepository.GetAll().ToListAsync();
            logger.LogInformation("Process podcasts");
            var podcastEpisodes =
                podcasts.SelectMany(p => p.Episodes.Select(e => new PodcastEpisode(p, e)));
            podcastEpisodes = podcastEpisodes.Skip(request.ItemsToSkip).Take(request.ItemsToTake.Value);
            var result = await shortnerService.Write(podcastEpisodes);
            if (!result.Success)
            {
                logger.LogError("Failure");
            }
            else
            {
                logger.LogInformation("Success");
            }
        }
        else if (request.EpisodeId != null)
        {
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == request.EpisodeId));
            if (podcast == null)
            {
                throw new ArgumentException($"No podcast found with episode-id '{request.EpisodeId!.Value}'.",
                    nameof(request.EpisodeId));
            }

            var result = await shortnerService.Write(
                new PodcastEpisode(podcast, podcast.Episodes.Single(x => x.Id == request.EpisodeId)), request.IsDryRun
            );
        } else if (request.Key != null)
        {
            var result = await shortnerService.Read(request.Key);
        }
        else
        {
            throw new ArgumentException("No operation specified");
        }
    }
}