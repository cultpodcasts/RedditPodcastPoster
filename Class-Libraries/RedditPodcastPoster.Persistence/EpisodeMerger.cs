using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Episode = RedditPodcastPoster.Models.Episode;
using Podcast = RedditPodcastPoster.Models.Podcast;

namespace RedditPodcastPoster.Persistence;

public class EpisodeMerger(IEpisodeMatcher episodeMatcher) : IEpisodeMerger
{
    public EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge)
    {
        Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, Podcast.EpisodeMatchFlags);
        }

        var existingList = existingEpisodes.ToList();
        var addedEpisodes = new List<Episode>();
        var mergedEpisodes = new List<(Episode Existing, Episode NewDetails)>();
        var failedEpisodes = new List<IEnumerable<Episode>>();
        var episodesToSave = new List<Episode>();

        foreach (var episodeToMerge in episodesToMerge)
        {
            var matchingExisting = existingList.Where(x => Match(x, episodeToMerge, episodeMatchRegex)).ToList();

            if (matchingExisting.Count <= 1)
            {
                var existingEpisode = matchingExisting.SingleOrDefault();
                if (existingEpisode == null)
                {
                    // New episode
                    episodeToMerge.Id = Guid.NewGuid();
                    var (updatedPodcastProperties, updatedTimestamp) = episodeToMerge.SetPodcastProperties(podcast);
                    addedEpisodes.Add(episodeToMerge);
                    episodesToSave.Add(episodeToMerge);
                    existingList.Add(episodeToMerge); // Add to list for subsequent matching
                }
                else
                {
                    // Merge with existing
                    var updated = MergeInPlace(existingEpisode, episodeToMerge);
                    var (updatedPodcastProperties, updatedTimestamp) = existingEpisode.SetPodcastProperties(podcast);

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


    private bool Match(Episode episode, Episode episodeToMerge, Regex? episodeMatchRegex)
    {
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId) && !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            if (episode.SpotifyId == episodeToMerge.SpotifyId)
            {
                return true;
            }

            return false;
        }

        if (!string.IsNullOrWhiteSpace(episode.YouTubeId) && !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            if (episode.YouTubeId == episodeToMerge.YouTubeId)
            {
                return true;
            }

            return false;
        }

        if (episode.AppleId.HasValue && episodeToMerge.AppleId.HasValue)
        {
            if (episode.AppleId.Value == episodeToMerge.AppleId.Value)
            {
                return true;
            }

            return false;
        }

        return episodeMatcher.IsMatch(episode, episodeToMerge, episodeMatchRegex);
    }

    private bool MergeInPlace(Episode existingEpisode, Episode episodeToMerge)
    {
        var updated = false;
        if (existingEpisode.Urls.Spotify == null && episodeToMerge.Urls.Spotify != null)
        {
            existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
            updated = true;
        }

        if (existingEpisode.Images?.Spotify == null && episodeToMerge.Images?.Spotify != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.Spotify ??= episodeToMerge.Images.Spotify;
            updated = true;
        }

        if (existingEpisode.Urls.Apple == null && episodeToMerge.Urls.Apple != null)
        {
            existingEpisode.Urls.Apple ??= episodeToMerge.Urls.Apple;
            updated = true;
        }

        if (existingEpisode.Images?.Apple == null && episodeToMerge.Images?.Apple != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.Apple ??= episodeToMerge.Images.Apple;
            updated = true;
        }

        if (existingEpisode.Urls.YouTube == null && episodeToMerge.Urls.YouTube != null)
        {
            existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
            updated = true;
        }

        if (existingEpisode.Images?.YouTube == null && episodeToMerge.Images?.YouTube != null)
        {
            existingEpisode.Images ??= new EpisodeImages();
            existingEpisode.Images.YouTube ??= episodeToMerge.Images.YouTube;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
            updated = true;
        }

        if (existingEpisode.AppleId == null && episodeToMerge.AppleId != null)
        {
            existingEpisode.AppleId = episodeToMerge.AppleId;
            updated = true;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
            updated = true;
        }

        if (existingEpisode.Description.EndsWith("...") &&
            existingEpisode.Description.Length < episodeToMerge.Description.Length)
        {
            existingEpisode.Description = episodeToMerge.Description;
            updated = true;
        }

        if (existingEpisode.Release.TimeOfDay == TimeSpan.Zero &&
            episodeToMerge.Release.TimeOfDay > TimeSpan.Zero)
        {
            existingEpisode.Release = episodeToMerge.Release;
            updated = true;
        }

        return updated;
    }
}