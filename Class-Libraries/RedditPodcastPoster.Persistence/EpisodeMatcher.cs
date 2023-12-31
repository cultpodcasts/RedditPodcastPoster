﻿using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EpisodeMatcher : IEpisodeMatcher
{
    private readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;
    private readonly ILogger<EpisodeMatcher> _logger;

    public EpisodeMatcher(ILogger<EpisodeMatcher> logger)
    {
        _logger = logger;
    }

    public bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex)
    {
        if (episodeMatchRegex == null)
        {
            return episodeToMerge.Title == existingEpisode.Title;
        }

        var episodeToMergeMatch = episodeMatchRegex.Match(episodeToMerge.Title);
        var episodeMatch = episodeMatchRegex.Match(existingEpisode.Title);

        if (episodeToMergeMatch.Groups["episodematch"].Success &&
            episodeMatch.Groups["episodematch"].Success)
        {
            var episodeToMergeUniqueMatch = episodeToMergeMatch.Groups["episodematch"].Value;
            var episodeUniqueMatch = episodeMatch.Groups["episodematch"].Value;
            var isMatch = episodeToMergeUniqueMatch == episodeUniqueMatch;
            return isMatch;
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

        var publishDifference = existingEpisode.Release - episodeToMerge.Release;
        if (Math.Abs(publishDifference.Ticks) < TimeSpan.FromMinutes(5).Ticks && Math.Abs(
                (existingEpisode.Length -
                 episodeToMerge.Length).Ticks) < TimeSpan.FromMinutes(1).Ticks)
        {
            return true;
        }

        return false;
    }
}