using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProvider(
    IAppleEpisodeRetrievalHandler appleEpisodeRetrievalHandler,
    IYouTubeEpisodeRetrievalHandler youTubeEpisodeRetrievalHandler,
    ISpotifyEpisodeRetrievalHandler spotifyEpisodeRetrievalHandler,
    IFoundEpisodeFilter foundEpisodeFilter,
    ILogger<EpisodeProvider> logger)
    : IEpisodeProvider
{
    public async Task<IList<Episode>> GetEpisodes(Podcast podcast, IEnumerable<Episode> episodes,
        IndexingContext indexingContext)
    {
        IList<Episode>? newEpisodes = null;
        var handled = false;
        if (indexingContext.IndexSpotify && podcast.ReleaseAuthority is null or Service.Spotify &&
            !string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            (newEpisodes, handled) = await spotifyEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext);
            if (handled)
            {
                logger.LogInformation(
                    "Get Episodes for podcast '{podcastName}' handled by '{spotifyEpisodeRetrievalHandlerName}'.",
                    podcast.Name, nameof(ISpotifyEpisodeRetrievalHandler));
            }
        }

        if (!handled && podcast.ReleaseAuthority != Service.YouTube && podcast.AppleId != null)
        {
            (newEpisodes, handled) = await appleEpisodeRetrievalHandler.GetEpisodes(podcast, indexingContext);
            if (handled)
            {
                logger.LogInformation(
                    "Get Episodes for podcast '{podcastName}' handled by '{appleEpisodeRetrievalHandlerName}'.",
                    podcast.Name, nameof(IAppleEpisodeRetrievalHandler));
            }
        }

        if (!handled || (podcast.ReleaseAuthority is Service.YouTube &&
                         !string.IsNullOrWhiteSpace(podcast.YouTubeChannelId)))
        {
            (newEpisodes, handled) =
                await youTubeEpisodeRetrievalHandler.GetEpisodes(podcast, episodes, indexingContext);
            if (handled)
            {
                logger.LogInformation(
                    "Get Episodes for podcast '{podcastName}' handled by '{youTubeEpisodeRetrievalHandlerName}'.",
                    podcast.Name, nameof(IYouTubeEpisodeRetrievalHandler));
            }
        }

        if (!handled)
        {
            logger.LogInformation(
                "Unable to handle podcast with name: '{podcastName}', id: {podcastId}. Spotify-Id: '{podcastSpotifyId}', Apple-Id: '{podcastAppleId}', YouTube-ChannelId: '{podcastYouTubeChannelId}', YouTube-PlayListId: '{podcastYouTubePlaylistId}'. Expensive-Queries? {hasExpensiveSpotifyEpisodesQueryName}= '{HasExpensiveSpotifyEpisodesQuery}', {HasExpensiveYouTubePlaylistQueryName}= '{HasExpensiveYouTubePlaylistQuery}'.",
                podcast.Name, podcast.Id, podcast.SpotifyId, podcast.AppleId, podcast.YouTubeChannelId,
                podcast.YouTubePlaylistId, nameof(podcast.HasExpensiveSpotifyEpisodesQuery),
                podcast.HasExpensiveSpotifyEpisodesQuery(), nameof(podcast.HasExpensiveYouTubePlaylistQuery),
                podcast.HasExpensiveYouTubePlaylistQuery());
        }

        if (newEpisodes != null && newEpisodes.Any() && !podcast.IndexAllEpisodes &&
            !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
        {
            newEpisodes = foundEpisodeFilter.ReduceEpisodes(podcast, newEpisodes);
        }

        return newEpisodes ?? new List<Episode>();
    }
}