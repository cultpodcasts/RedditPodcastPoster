using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Handlers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Handlers;

public class AppleEpisodeRetrievalHandler(
    IAppleEpisodeProvider appleEpisodeProvider,
    ILogger<AppleEpisodeRetrievalHandler> logger)
    : IAppleEpisodeRetrievalHandler
{
    private readonly ILogger<AppleEpisodeRetrievalHandler> _logger = logger;

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;
        IList<Episode> newEpisodes = new List<Episode>();
        if (podcast.AppleId != null)
        {
            var foundEpisodes = await appleEpisodeProvider.GetEpisodes(
                new ApplePodcastId(podcast.AppleId.Value), indexingContext);
            if (foundEpisodes != null)
            {
                newEpisodes = foundEpisodes;
            }

            handled = true;
        }

        return new EpisodeRetrievalHandlerResponse(newEpisodes, handled);
    }
}
