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
        bool ignoreAppleGracePeriod = false,
        int? maxPosts = int.MaxValue)
    {
        var podcasts = new List<Podcast>();
        foreach (var podcastId in podcastIds)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast != null)
            {
                podcasts.Add(podcast);
            }
        }

        var matchingPodcastEpisodes =
            podcastEpisodeFilter.GetNewEpisodesReleasedSince(podcasts, since, youTubeRefreshed, spotifyRefreshed)
                .ToArray();

        if (!matchingPodcastEpisodes.Any())
        {
            return [];
        }

        var updatedPodcasts = new List<Podcast>();
        var postedEpisodes = new List<Episode>();

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        var posted = 0;
        var failures = 0;
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes.OrderBy(x => x.Episode.Release))
        {
            if (posted >= maxPosts)
            {
                break;
            }

            if (!matchingPodcastEpisode.Episode.Posted)
            {
                if (matchingPodcastEpisode.Episode.Length >=
                    (matchingPodcastEpisode.Podcast.MinimumDuration ?? _postingCriteria.MinimumDuration) ||
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
                            var result =
                                await podcastEpisodePoster.PostPodcastEpisode(matchingPodcastEpisode, preferYouTube);
                            if (result.Success)
                            {
                                posted++;
                                postedEpisodes.Add(matchingPodcastEpisode.Episode);
                                updatedPodcasts.Add(matchingPodcastEpisode.Podcast);
                            }
                            else
                            {
                                failures++;
                                if (failures >= 5)
                                {
                                    logger.LogError("Reddit posting failures = {failures}.", failures);
                                    break;
                                }
                            }

                            matchingPodcastEpisodeResults.Add(result);
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
                    updatedPodcasts.Add(matchingPodcastEpisode.Podcast);
                    matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
                }
            }
        }

        foreach (var podcast in updatedPodcasts)
        {
            await podcastRepository.Save(podcast);
        }

        logger.LogInformation(
            "{method}: Updated podcasts with ids {ids}. Episodes posted {postCount} (actual-posted: {postedCount}).",
            nameof(PostNewEpisodes),
            string.Join(',', updatedPodcasts.Select(x => x.Id)),
            postedEpisodes.Count,
            postedEpisodes.Count(x => x.Posted));

        return matchingPodcastEpisodeResults;
    }
}