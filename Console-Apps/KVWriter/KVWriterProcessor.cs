using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.UrlShortening;

namespace KVWriter;

public class KVWriterProcessor(
    IPodcastRepository podcastRepository,
    IShortnerService shortnerService
)
{
    public async Task Process(KVWriterRequest request)
    {
        if (request.ItemsToTake != null)
        {
            var podcasts = await podcastRepository.GetAll().ToListAsync();
            var podcastEpisodes =
                podcasts.SelectMany(p => p.Episodes.Select(e => new PodcastEpisode(p, e)));
            podcastEpisodes = podcastEpisodes.Take(request.ItemsToTake.Value);
            var result = await shortnerService.Write(podcastEpisodes);
        }
        else if (request.EpisodeId != null)
        {
            var podcast = await podcastRepository.GetBy(x => x.Episodes.Any(ep => ep.Id == request.EpisodeId));
            if (podcast == null)
            {
                throw new ArgumentException($"No podcast found with episode-id '{request.EpisodeId!.Value}'.",
                    nameof(request.EpisodeId));
            }

            var result = await shortnerService.Write([
                new PodcastEpisode(podcast, podcast.Episodes.Single(x => x.Id == request.EpisodeId))
            ]);
        }
        else
        {
            throw new ArgumentException("No operation specified");
        }
    }
}