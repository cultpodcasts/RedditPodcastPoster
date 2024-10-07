using Microsoft.Extensions.Logging;
using RedditPodcastPoster.CloudflareRedirect;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RenamePodcast;

public class RenamePodcastProcessor(
    IPodcastRepository podcastRepository,
    IRedirectService redirectService,
    ILogger<RenamePodcastProcessor> logger)
{
    public async Task Process(RenamePodcastRequest request)
    {
        var podcasts = await podcastRepository.GetAllBy(x => x.Name == request.OldPodcastName).ToListAsync();
        if (!podcasts.Any())
        {
            throw new InvalidOperationException($"Podcast not found with name '{request.OldPodcastName}'.");
        }

        var result =
            await redirectService.CreatePodcastRedirect(
                new PodcastRedirect(
                    request.OldPodcastName,
                    request.NewPodcastName));
        if (result)
        {
            foreach (var podcast in podcasts)
            {
                podcast.Name = request.NewPodcastName;
                await podcastRepository.Save(podcast);
            }
        }
    }
}