﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class YouTubeEpisodeRetrievalHandler : IYouTubeEpisodeRetrievalHandler
{
    private readonly ILogger<YouTubeEpisodeRetrievalHandler> _logger;
    private readonly IYouTubeEpisodeProvider _youTubeEpisodeProvider;

    public YouTubeEpisodeRetrievalHandler(
        IYouTubeEpisodeProvider youTubeEpisodeProvider,
        ILogger<YouTubeEpisodeRetrievalHandler> logger)
    {
        _youTubeEpisodeProvider = youTubeEpisodeProvider;
        _logger = logger;
    }

    public async Task<EpisodeRetrievalHandlerResponse> GetEpisodes(Podcast podcast, IndexingContext indexingContext)
    {
        var handled = false;
        IList<Episode> episodes= new List<Episode>();
        if (podcast.ReleaseAuthority is Service.YouTube || !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId))
        {
            if (!string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
            {
                if (podcast.HasExpensiveYouTubePlaylistQuery() && indexingContext.SkipExpensiveQueries)
                {
                    _logger.LogInformation(
                        $"Podcast '{podcast.Id}' has known expensive query and will not run this time.");
                    {
                        return new EpisodeRetrievalHandlerResponse(episodes, handled);
                    }
                }

                var getPlaylistEpisodesResult = await _youTubeEpisodeProvider.GetPlaylistEpisodes(
                    new YouTubePlaylistId(podcast.YouTubePlaylistId), indexingContext);
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

                var foundEpisodes = await _youTubeEpisodeProvider.GetEpisodes(
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