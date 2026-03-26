using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodesPoster(
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

    public async Task<PostingResult> PostNewEpisodes(
        DateTime since,
        IEnumerable<PodcastEpisode> podcastEpisodes,
        bool youTubeRefreshed = true,
        bool spotifyRefreshed = true,
        bool preferYouTube = false,
        bool ignoreAppleGracePeriod = false,
        int? maxPosts = int.MaxValue)
    {
        var candidatePodcastEpisodes = podcastEpisodes
            .Where(pe => pe.Episode.Release >= since && pe.Episode is { Posted: false, Ignored: false, Removed: false })
            .ToList();

        var matchingPodcastEpisodes =
            (await podcastEpisodeFilter.GetNewEpisodesReleasedSince(candidatePodcastEpisodes, since, youTubeRefreshed,
                spotifyRefreshed)).ToArray();

        if (!matchingPodcastEpisodes.Any())
        {
            return new PostingResult([], []);
        }

        var modifiedPodcastEpisodes = new HashSet<PodcastEpisode>();
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
                                modifiedPodcastEpisodes.Add(matchingPodcastEpisode);
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
                    modifiedPodcastEpisodes.Add(matchingPodcastEpisode);
                    matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
                }
            }
        }

        return new PostingResult(matchingPodcastEpisodeResults, modifiedPodcastEpisodes);
    }
}
