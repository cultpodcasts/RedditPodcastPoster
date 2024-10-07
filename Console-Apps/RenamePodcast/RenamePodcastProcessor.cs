using Microsoft.Extensions.Logging;
using RedditPodcastPoster.CloudflareRedirect;

namespace RenamePodcast;

public class RenamePodcastProcessor(IRedirectService redirectService, ILogger<RenamePodcastProcessor> logger)
{
    public async Task Process(RenamePodcastRequest request)
    {
        var redirects = await redirectService.GetAllPodcastRedirects();
        foreach (var redirect in redirects)
        {
            logger.LogInformation($"from: '{redirect.OldPodcastName}' -> '{redirect.NewPodcastName}'.");
        }
    }
}