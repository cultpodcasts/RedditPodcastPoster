using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices;

public class EpisodeMerger(
    IEpisodePlatformMatcher platformMatcher,
    IEpisodePlatformMerger platformMerger) : IEpisodeMerger
{
    public EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge)
    {
        System.Text.RegularExpressions.Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new System.Text.RegularExpressions.Regex(
                podcast.EpisodeMatchRegex,
                Podcast.EpisodeMatchFlags);
        }

        var existingList = existingEpisodes.ToList();
        var addedEpisodes = new List<Episode>();
        var mergedEpisodes = new List<(Episode Existing, Episode NewDetails)>();
        var failedEpisodes = new List<IEnumerable<Episode>>();
        var episodesToSave = new List<Episode>();

        foreach (var episodeToMerge in episodesToMerge)
        {
            var matchingExisting = existingList
                .Where(x => platformMatcher.IsMatch(
                    x,
                    episodeToMerge,
                    episodeMatchRegex,
                    podcast,
                    existingList))
                .ToList();

            if (matchingExisting.Count <= 1)
            {
                var existingEpisode = matchingExisting.SingleOrDefault();
                if (existingEpisode == null)
                {
                    episodeToMerge.Id = Guid.NewGuid();
                    episodeToMerge.SetPodcastProperties(podcast);
                    addedEpisodes.Add(episodeToMerge);
                    episodesToSave.Add(episodeToMerge);
                    existingList.Add(episodeToMerge);
                }
                else
                {
                    var updated = platformMerger.MergeInPlace(existingEpisode, episodeToMerge, podcast);
                    existingEpisode.SetPodcastProperties(podcast);

                    if (updated)
                    {
                        mergedEpisodes.Add((Existing: existingEpisode, NewDetails: episodeToMerge));
                        episodesToSave.Add(existingEpisode);
                    }
                }
            }
            else
            {
                failedEpisodes.Add(matchingExisting);
            }
        }

        return new EpisodeMergeResult(episodesToSave, addedEpisodes, mergedEpisodes, failedEpisodes);
    }
}
