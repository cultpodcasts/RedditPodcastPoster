using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Episode;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Handlers;

public class YouTubeEpisodeRetrievalHandler(
    IYouTubeEpisodeProvider youTubeEpisodeProvider,
    ILogger<YouTubeEpisodeRetrievalHandler> logger)
    : IYouTubeEpisodeRetrievalHandler
{
    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;
        IList<RedditPodcastPoster.Models.Episode> episodes = new List<RedditPodcastPoster.Models.Episode>();
        if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
            {
                if (podcast.HasExpensiveYouTubePlaylistQuery() && indexingContext.SkipExpensiveYouTubeQueries)
                {
                    logger.LogInformation(
                        "Podcast '{PodcastId}' has known expensive query and will not run this time.", podcast.Id);
                    {
                        return new EpisodeRetrievalHandlerResponse(episodes, handled);
                    }
                }

                var getPlaylistEpisodesResult = await youTubeEpisodeProvider.GetPlaylistEpisodes(
                    new YouTubePlaylistId(podcast.YouTubePlaylistId), new YouTubeChannelId(podcast.YouTubeChannelId),
                    indexingContext);
                if (getPlaylistEpisodesResult.Results != null)
                {
                    episodes = getPlaylistEpisodesResult.Results;
                }

                if (getPlaylistEpisodesResult.IsExpensiveQuery)
                {
                    podcast.YouTubePlaylistQueryIsExpensive = true;
                }
            }
            else
            {
                IEnumerable<string> knownIds;
                if (indexingContext.ReleasedSince.HasValue)
                {
                    knownIds = podcast.Episodes.Where(x => x.Release >= indexingContext.ReleasedSince)
                        .Select(x => x.YouTubeId);
                }
                else
                {
                    knownIds = podcast.Episodes.Select(x => x.YouTubeId);
                }

                var foundEpisodes = await youTubeEpisodeProvider.GetEpisodes(
                    new YouTubeChannelId(podcast.YouTubeChannelId), indexingContext, knownIds);
                if (foundEpisodes != null)
                {
                    episodes = foundEpisodes;
                }
            }

            handled = true;
        }

        return new EpisodeRetrievalHandlerResponse(episodes, handled);
    }
}