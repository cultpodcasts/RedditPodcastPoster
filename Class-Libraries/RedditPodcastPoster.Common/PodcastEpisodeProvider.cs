using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common;

public class PodcastEpisodeProvider(
    IPodcastRepository repository,
    IPodcastEpisodeFilter podcastEpisodeFilter
) : IPodcastEpisodeProvider
{
    public async Task<IEnumerable<PodcastEpisode>> GetUntweetedPodcastEpisodes(
        bool youTubeRefreshed,
        bool spotifyRefreshed)
    {
        var numberOfDays = 7;
        var podcastEpisodes = new List<PodcastEpisode>();

        var untweetedPodcastIds =
            await repository.GetPodcastIdsWithUntweetedReleasedSince(DateTimeExtensions.DaysAgo(numberOfDays));

        foreach (var untweetedPodcastId in untweetedPodcastIds)
        {
            var podcast = await repository.GetPodcast(untweetedPodcastId);
            var filtered = podcastEpisodeFilter.GetMostRecentUntweetedEpisodes(
                podcast, youTubeRefreshed, spotifyRefreshed, numberOfDays);
            podcastEpisodes.AddRange(filtered);
        }

        return podcastEpisodes.OrderByDescending(x => x.Episode.Release);
    }
}