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
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PodcastEpisodesPoster> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IPodcastEpisodesPoster
{
    private static readonly TimeSpan AppleDelay = TimeSpan.FromHours(1);
    private readonly PostingCriteria _postingCriteria = postingCriteria.Value;

    public async Task<IList<ProcessResponse>> PostNewEpisodes(
        DateTime since,
        IEnumerable<Guid> podcastIds,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false,
        bool ignoreAppleGracePeriod = false)
    {
        var podcasts = new List<Podcast>();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            podcasts.Add(podcast);
        }

        var matchingPodcastEpisodes =
            podcastEpisodeFilter.GetNewEpisodesReleasedSince(podcasts, since, youTubeRefreshed, spotifyRefreshed);

        if (!matchingPodcastEpisodes.Any())
        {
            return Array.Empty<ProcessResponse>();
        }

        var UpdatedPodcasts = new List<Podcast>();

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes.OrderBy(x => x.Episode.Release))
        {
            if (!matchingPodcastEpisode.Episode.Posted)
            {
                if (matchingPodcastEpisode.Episode.Length >= _postingCriteria.MinimumDuration ||
                    (matchingPodcastEpisode.Podcast.BypassShortEpisodeChecking.HasValue &&
                     matchingPodcastEpisode.Podcast.BypassShortEpisodeChecking.Value))
                {
                    if (matchingPodcastEpisode.Episode.Urls.Spotify != null ||
                        matchingPodcastEpisode.Episode.Urls.YouTube != null)
                    {
                        var appleGracePeriodEnds = DateTimeOffset
                            .FromUnixTimeSeconds(matchingPodcastEpisode.Podcast.Timestamp).UtcDateTime.Add(AppleDelay);
                        if (ignoreAppleGracePeriod ||
                            matchingPodcastEpisode.Podcast.AppleId == null ||
                            matchingPodcastEpisode.Episode.AppleId != null ||
                            DateTime.UtcNow >= appleGracePeriodEnds)
                        {
                            var result = await podcastEpisodePoster.PostPodcastEpisode(
                                matchingPodcastEpisode, preferYouTube);
                            matchingPodcastEpisodeResults.Add(result);
                            if (result.Success)
                            {
                                UpdatedPodcasts.Add(matchingPodcastEpisode.Podcast);
                            }
                        }
                        else
                        {
                            matchingPodcastEpisodeResults.Add(ProcessResponse.DelayedPosting(
                                $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was delayed posting as Apple-Id is null. Podcast updated '{DateTimeOffset.FromUnixTimeSeconds(matchingPodcastEpisode.Podcast.Timestamp).UtcDateTime:g}', Grace-Period until '{appleGracePeriodEnds:g}'."));
                        }
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
                    UpdatedPodcasts.Add(matchingPodcastEpisode.Podcast);
                    matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
                }
            }
        }

        foreach (var podcast in UpdatedPodcasts)
        {
            await podcastRepository.Save(podcast);
        }

        return matchingPodcastEpisodeResults;
    }
}