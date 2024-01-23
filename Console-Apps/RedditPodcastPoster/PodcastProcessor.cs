using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster;

public class PodcastProcessor(
    IPodcastsUpdater podcastsUpdater,
    IEpisodeProcessor episodeProcessor,
    ILogger<PodcastProcessor> logger)
{
    public async Task<ProcessResponse> Process(ProcessRequest processRequest)
    {
        var youTubeRefreshed = true;
        var spotifyRefreshed = false;

        if (processRequest.RefreshEpisodes)
        {
            IndexingContext indexingContext = new(
                processRequest.ReleaseBaseline,
                processRequest.SkipYouTube,
                processRequest.SkipSpotify,
                processRequest.SkipExpensiveQueries);

            var originalSkipYouTubeUrlResolving = indexingContext.SkipYouTubeUrlResolving;
            var originalSkipSpotifyUrlResolving = indexingContext.SkipSpotifyUrlResolving;

            var results = await podcastsUpdater.UpdatePodcasts(indexingContext);
            if (!results)
            {
                logger.LogError("Failure occurred.");
            }

            youTubeRefreshed = originalSkipYouTubeUrlResolving == false &&
                               originalSkipYouTubeUrlResolving == indexingContext.SkipYouTubeUrlResolving;
            spotifyRefreshed = originalSkipSpotifyUrlResolving == false &&
                               originalSkipSpotifyUrlResolving == indexingContext.SkipSpotifyUrlResolving;
        }

        if (processRequest.ReleaseBaseline != null)
        {
            return await episodeProcessor.PostEpisodesSinceReleaseDate(
                processRequest.ReleaseBaseline.Value,
                youTubeRefreshed,
                spotifyRefreshed);
        }

        return ProcessResponse.Successful("Operation successful.");
    }
}