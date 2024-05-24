using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleEpisodeRetrievalHandler(
    IAppleEpisodeProvider appleEpisodeProvider,
    ILogger<AppleEpisodeRetrievalHandler> logger)
    : IAppleEpisodeRetrievalHandler
{
    private readonly ILogger<AppleEpisodeRetrievalHandler> _logger = logger;

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;
        IList<Episode> episodes = new List<Episode>();
        if (podcast.AppleId != null)
        {
            var foundEpisodes = await appleEpisodeProvider.GetEpisodes(
                new ApplePodcastId(podcast.AppleId.Value), indexingContext);
            if (foundEpisodes != null)
            {
                episodes = foundEpisodes;
            }

            handled = true;
        }

        return new EpisodeRetrievalHandlerResponse(episodes, handled);
    }
}