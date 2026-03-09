using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlShortening;
using V2Podcast = RedditPodcastPoster.Models.V2.Podcast;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace KVWriter;

public class KVWriterProcessor(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
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

            var podcastEpisodes = new List<PodcastEpisode>();
            foreach (var podcast in podcasts)
            {
                var episodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();
                podcastEpisodes.AddRange(episodes.Select(e => CreatePodcastEpisode(podcast, e)));
            }

            var batch = podcastEpisodes.Skip(request.ItemsToSkip).Take(request.ItemsToTake.Value);
            var result = await shortnerService.Write(batch);
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
            var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId.Value);
            if (episode == null)
            {
                throw new ArgumentException($"No episode found with episode-id '{request.EpisodeId.Value}'.",
                    nameof(request.EpisodeId));
            }

            var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
            if (podcast == null)
            {
                throw new ArgumentException($"No podcast found with podcast-id '{episode.PodcastId}' for episode-id '{request.EpisodeId.Value}'.",
                    nameof(request.EpisodeId));
            }

            var result = await shortnerService.Write(CreatePodcastEpisode(podcast, episode), request.IsDryRun);
        }
        else if (request.Key != null)
        {
            var result = await shortnerService.Read(request.Key);
        }
        else
        {
            throw new ArgumentException("No operation specified");
        }
    }

    private static PodcastEpisode CreatePodcastEpisode(V2Podcast podcast, V2Episode episode)
    {
        var servicePodcast = new Podcast(podcast.Id)
        {
            Name = podcast.Name
        };

        var serviceEpisode = new Episode
        {
            Id = episode.Id,
            Title = episode.Title,
            Release = episode.Release,
            Length = episode.Length
        };

        return new PodcastEpisode(servicePodcast, serviceEpisode);
    }
}