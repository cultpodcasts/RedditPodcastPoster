using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Persistence;

public class EpisodeMatcher(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EpisodeMatcher> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IEpisodeMatcher
{
    private const int MinFuzzyTitleScore = 70;
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan YouTubeFirstCrossPlatformDurationTolerance = TimeSpan.FromMinutes(5);

    private readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;

    public bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex, Podcast podcast)
    {
        if (episodeMatchRegex == null)
        {
            if (episodeToMerge.Title == existingEpisode.Title)
            {
                return true;
            }

            return MatchesByDefaultHeuristics(existingEpisode, episodeToMerge, podcast);
        }

        var episodeToMergeMatch = episodeMatchRegex.Match(episodeToMerge.Title);
        var episodeMatch = episodeMatchRegex.Match(existingEpisode.Title);

        if (episodeToMergeMatch.Groups["episodematch"].Success &&
            episodeMatch.Groups["episodematch"].Success)
        {
            var episodeToMergeUniqueMatch = episodeToMergeMatch.Groups["episodematch"].Value;
            var episodeUniqueMatch = episodeMatch.Groups["episodematch"].Value;
            var isMatch = episodeToMergeUniqueMatch == episodeUniqueMatch;
            if (isMatch)
            {
                return true;
            }

            return false;
        }

        if (episodeToMergeMatch.Groups["title"].Success && episodeMatch.Groups["title"].Success)
        {
            var episodeToMergeTitle = episodeToMergeMatch.Groups["title"].Value;
            var episodeTitle = episodeMatch.Groups["title"].Value;
            var isMatch = episodeToMergeTitle == episodeTitle;
            if (isMatch)
            {
                return true;
            }

            if (_compareInfo.Compare(episodeToMergeTitle, episodeTitle, CompareOptions.IgnoreSymbols) == 0)
            {
                return true;
            }
        }

        return MatchesByDefaultHeuristics(existingEpisode, episodeToMerge, podcast);
    }

    private static bool MatchesByDefaultHeuristics(
        Episode existingEpisode,
        Episode episodeToMerge,
        Podcast podcast)
    {
        if (MatchesByFuzzyTitleAndDuration(existingEpisode, episodeToMerge, podcast))
        {
            return true;
        }

        return MatchesByReleaseAndDuration(existingEpisode, episodeToMerge, podcast);
    }

    private static bool MatchesByFuzzyTitleAndDuration(
        Episode existingEpisode,
        Episode episodeToMerge,
        Podcast podcast)
    {
        if (!FuzzyMatcher.IsMatch(existingEpisode.Title, episodeToMerge, e => e.Title, MinFuzzyTitleScore))
        {
            return false;
        }

        return DurationsMatch(existingEpisode, episodeToMerge, podcast);
    }

    private static bool MatchesByReleaseAndDuration(
        Episode existingEpisode,
        Episode episodeToMerge,
        Podcast podcast)
    {
        if (!EpisodeReleaseMatchTolerance.EpisodesReleaseMatch(podcast, existingEpisode, episodeToMerge))
        {
            return false;
        }

        if (!DurationsMatch(existingEpisode, episodeToMerge, podcast))
        {
            return false;
        }

        if (podcast.YouTubePublishingDelay().Ticks < 0)
        {
            return FuzzyMatcher.IsMatch(existingEpisode.Title, episodeToMerge, e => e.Title, MinFuzzyTitleScore);
        }

        return true;
    }

    private static bool DurationsMatch(Episode existingEpisode, Episode episodeToMerge, Podcast podcast)
    {
        var tolerance = GetDurationTolerance(existingEpisode, episodeToMerge, podcast);
        return Math.Abs((existingEpisode.Length - episodeToMerge.Length).Ticks) < tolerance.Ticks;
    }

    private static TimeSpan GetDurationTolerance(Episode existingEpisode, Episode episodeToMerge, Podcast podcast)
    {
        if (podcast.YouTubePublishingDelay().Ticks < 0 &&
            HasCrossPlatformYouTubeSpotifyPair(existingEpisode, episodeToMerge))
        {
            return YouTubeFirstCrossPlatformDurationTolerance;
        }

        return DurationTolerance;
    }

    private static bool HasCrossPlatformYouTubeSpotifyPair(Episode existingEpisode, Episode episodeToMerge)
    {
        var existingYouTube = HasYouTubeIdentity(existingEpisode);
        var existingSpotify = HasSpotifyIdentity(existingEpisode);
        var incomingYouTube = HasYouTubeIdentity(episodeToMerge);
        var incomingSpotify = HasSpotifyIdentity(episodeToMerge);

        return (existingYouTube && !existingSpotify && incomingSpotify && !incomingYouTube) ||
               (incomingYouTube && !incomingSpotify && existingSpotify && !existingYouTube);
    }

    private static bool HasYouTubeIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube != null;

    private static bool HasSpotifyIdentity(Episode episode) =>
        !string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null;
}
