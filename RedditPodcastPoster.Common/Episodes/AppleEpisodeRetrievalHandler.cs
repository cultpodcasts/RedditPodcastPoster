using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

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

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext,
        IList<Episode> episodes)
    {
        var foundEpisodes = await _appleEpisodeProvider.GetEpisodes(
            new ApplePodcastId(podcast.AppleId.Value), indexingContext);
        if (foundEpisodes != null)
        {
            episodes = foundEpisodes;
        }

        var handled = true;
        return new EpisodeRetrievalHandlerResponse(episodes, handled);
    }
}