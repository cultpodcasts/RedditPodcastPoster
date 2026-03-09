using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using LegacyPodcast = RedditPodcastPoster.Models.Podcast;
using LegacyEpisode = RedditPodcastPoster.Models.Episode;
using V2Episode = RedditPodcastPoster.Models.V2.Episode;

namespace RedditPodcastPoster.Persistence;

public class EpisodeMerger(IEpisodeMatcher episodeMatcher) : IEpisodeMerger
{
    public async Task<EpisodeMergeResult> MergeEpisodes(
        LegacyPodcast podcast,
        IEnumerable<LegacyEpisode> existingEpisodes,
        IEnumerable<LegacyEpisode> episodesToMerge)
    {
        Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new Regex(podcast.EpisodeMatchRegex, LegacyPodcast.EpisodeMatchFlags);
        }

        var existingList = existingEpisodes.ToList();
        var addedEpisodes = new List<LegacyEpisode>();
        var mergedEpisodes = new List<(LegacyEpisode Existing, LegacyEpisode NewDetails)>();
        var failedEpisodes = new List<IEnumerable<LegacyEpisode>>();
        var episodesToSave = new List<V2Episode>();

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
                    addedEpisodes.Add(episodeToMerge);
                    episodesToSave.Add(ToV2Episode(podcast, episodeToMerge));
                    existingList.Add(episodeToMerge); // Add to list for subsequent matching
                }
                else
                {
                    // Merge with existing
                    var updated = MergeInPlace(existingEpisode, episodeToMerge);
                    if (updated)
                    {
                        mergedEpisodes.Add((Existing: existingEpisode, NewDetails: episodeToMerge));
                        episodesToSave.Add(ToV2Episode(podcast, existingEpisode));
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

    private bool Match(LegacyEpisode episode, LegacyEpisode episodeToMerge, Regex? episodeMatchRegex)
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

    private bool MergeInPlace(LegacyEpisode existingEpisode, LegacyEpisode episodeToMerge)
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

    private static V2Episode ToV2Episode(LegacyPodcast podcast, LegacyEpisode episode)
    {
        return new Models.V2.Episode
        {
            Id = episode.Id,
            PodcastId = podcast.Id,
            Title = episode.Title,
            Description = episode.Description,
            Release = episode.Release,
            Length = episode.Length,
            Explicit = episode.Explicit,
            Posted = episode.Posted,
            Tweeted = episode.Tweeted,
            BlueskyPosted = episode.BlueskyPosted,
            Ignored = episode.Ignored,
            Removed = episode.Removed,
            SpotifyId = episode.SpotifyId,
            AppleId = episode.AppleId,
            YouTubeId = episode.YouTubeId,
            Urls = episode.Urls,
            Subjects = episode.Subjects ?? [],
            SearchTerms = episode.SearchTerms,
            PodcastName = podcast.Name,
            PodcastSearchTerms = podcast.SearchTerms,
            SearchLanguage = episode.Language ?? podcast.Language,
            PodcastMetadataVersion = null,
            PodcastRemoved = podcast.Removed,
            Images = episode.Images,
            TwitterHandles = episode.TwitterHandles,
            BlueskyHandles = episode.BlueskyHandles
        };
    }
}
