using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodesPoster(
    IPodcastRepository podcastRepository,
    IPodcastEpisodeFilter podcastEpisodeFilter,
    IPodcastEpisodePoster podcastEpisodePoster,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<PodcastEpisodesPoster> logger)
    : IPodcastEpisodesPoster
{
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IList<ProcessResponse>> PostNewEpisodes(
        DateTime since,
        IEnumerable<Podcast> podcasts,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false)
    {
        var matchingPodcastEpisodes =
            podcastEpisodeFilter.GetNewEpisodesReleasedSince(podcasts, since, youTubeRefreshed, spotifyRefreshed);

        if (!matchingPodcastEpisodes.Any())
        {
            return Array.Empty<ProcessResponse>();
        }

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes.OrderByDescending(x => x.Episode.Release))
        {
            if (matchingPodcastEpisode.Episode.Length >= _postingCriteria.MinimumDuration ||
                (matchingPodcastEpisode.Podcast.BypassShortEpisodeChecking.HasValue &&
                 matchingPodcastEpisode.Podcast.BypassShortEpisodeChecking.Value))
            {
                if (matchingPodcastEpisode.Episode.Urls.Spotify != null ||
                    matchingPodcastEpisode.Episode.Urls.YouTube != null)
                {
                    var result = await podcastEpisodePoster.PostPodcastEpisode(
                        matchingPodcastEpisode, preferYouTube);
                    matchingPodcastEpisodeResults.Add(result);
                }
                else
                {
                    matchingPodcastEpisodeResults.Add(ProcessResponse.NoSuitableLink(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored as no Spotify or YouTube link."));
                }
            }
            else
            {
                matchingPodcastEpisode.Episode.Ignored = true;
                matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                    $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
            }
        }

        foreach (var podcast in matchingPodcastEpisodes.Select(x => x.Podcast).Distinct())
        {
            await podcastRepository.Save(podcast);
        }

        return matchingPodcastEpisodeResults;
    }
}