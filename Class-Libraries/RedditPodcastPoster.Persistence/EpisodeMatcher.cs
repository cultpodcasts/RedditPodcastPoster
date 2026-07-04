using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EpisodeMatcher(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<EpisodeMatcher> logger,
#pragma warning restore CS9113 // Parameter is unread.
    IEpisodePlatformMatcher platformMatcher) : IEpisodeMatcher
{
    public bool IsMatch(
        Episode existingEpisode,
        Episode episodeToMerge,
        Regex? episodeMatchRegex,
        Podcast podcast) =>
        platformMatcher.IsMatch(
            existingEpisode,
            episodeToMerge,
            episodeMatchRegex,
            podcast,
            [existingEpisode]);
}
