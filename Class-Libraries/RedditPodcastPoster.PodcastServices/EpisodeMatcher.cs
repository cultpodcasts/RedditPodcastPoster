using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;

namespace RedditPodcastPoster.PodcastServices;

/// Indexing orchestration matcher: delegates stored-episode merge decisions to <see cref="Episodes.Matching.IEpisodePlatformMatcher"/>.
/// Not to be confused with platform catalogue finders (<c>SpotifySearchResultFinder</c>, <c>YouTubeSearchResultFinder</c>).
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
