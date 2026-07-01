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
        if (MatchesByFuzzyTitleAndDuration(existingEpisode, episodeToMerge))
        {
            return true;
        }

        return MatchesByReleaseAndDuration(existingEpisode, episodeToMerge, podcast);
    }

    private static bool MatchesByFuzzyTitleAndDuration(Episode existingEpisode, Episode episodeToMerge)
    {
        if (!FuzzyMatcher.IsMatch(existingEpisode.Title, episodeToMerge, e => e.Title, MinFuzzyTitleScore))
        {
            return false;
        }

        return Math.Abs((existingEpisode.Length - episodeToMerge.Length).Ticks) < DurationTolerance.Ticks;
    }

    private static bool MatchesByReleaseAndDuration(
        Episode existingEpisode,
        Episode episodeToMerge,
        Podcast podcast)
    {
        var episodeLength = existingEpisode.Length > episodeToMerge.Length
            ? existingEpisode.Length
            : episodeToMerge.Length;
        var releaseToleranceTicks = EpisodeReleaseMatchTolerance.GetToleranceTicks(podcast, episodeLength);
        var publishDifference = existingEpisode.Release - episodeToMerge.Release;
        return Math.Abs(publishDifference.Ticks) < releaseToleranceTicks &&
               Math.Abs((existingEpisode.Length - episodeToMerge.Length).Ticks) < DurationTolerance.Ticks;
    }
}
