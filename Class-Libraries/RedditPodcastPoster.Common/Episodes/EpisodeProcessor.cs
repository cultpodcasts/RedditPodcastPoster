﻿using System.Linq.Expressions;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Adaptors;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor(
    IPodcastRepository podcastRepository,
    IPodcastEpisodesPoster podcastEpisodesPoster,
    IProcessResponsesAdaptor processResponsesAdaptor,
    ILogger<EpisodeProcessor> logger)
    : IEpisodeProcessor
{
    public static Expression<Func<Podcast, bool>> RecentRelease = x =>
        (!x.Removed.IsDefined() || x.Removed == false) &&
        x.Episodes.Any(
            episode =>
                episode.Release > DateTime.Now.AddDays(-30) &&
                episode.Posted == false &&
                episode.Ignored == false &&
                episode.Removed == false);


    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(
        DateTime since,
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        logger.LogInformation($"{nameof(PostEpisodesSinceReleaseDate)} Finding episodes released since '{since}'.");
        var podcasts = await podcastRepository.GetAllBy(RecentRelease).ToArrayAsync();

        var matchingPodcastEpisodeResults =
            await podcastEpisodesPoster.PostNewEpisodes(since, podcasts, youTubeRefreshed, spotifyRefreshed);

        return processResponsesAdaptor.CreateResponse(matchingPodcastEpisodeResults);
    }
}