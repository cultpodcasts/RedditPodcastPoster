using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleEpisodeRetrievalHandler : IAppleEpisodeRetrievalHandler
{
    private readonly IAppleEpisodeProvider _appleEpisodeProvider;
    private readonly ILogger<AppleEpisodeRetrievalHandler> _logger;

    public AppleEpisodeRetrievalHandler(
        IAppleEpisodeProvider appleEpisodeProvider,
        ILogger<AppleEpisodeRetrievalHandler> logger)
    {
        _appleEpisodeProvider = appleEpisodeProvider;
        _logger = logger;
    }

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;
        IList<Episode> episodes= new List<Episode>();
        if (podcast.AppleId != null)
        {
            var foundEpisodes = await _appleEpisodeProvider.GetEpisodes(
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