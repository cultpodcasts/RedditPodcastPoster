using KVWriter.Shortner;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace KVWriter;

public class KVWriterProcessor(
    IPodcastRepository podcastRepository,
    IShortnerService shortnerService
)
{
    public async Task Process(KVWriterRequest request)
    {
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        var podcastEpisodes =
            podcasts.SelectMany(p => p.Episodes.Select(e => new PodcastEpisode(p, e)));
        podcastEpisodes = podcastEpisodes.Take(request.ItemsToTake);
        var result = await shortnerService.Write(podcastEpisodes);
    }
}