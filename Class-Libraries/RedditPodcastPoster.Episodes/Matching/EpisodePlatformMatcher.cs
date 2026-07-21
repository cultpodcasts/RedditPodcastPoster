using System.Globalization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Episodes.Matching;

public sealed partial class EpisodePlatformMatcher(IEnumerable<IReleaseMatchStrategy> releaseMatchStrategies)
    : IEpisodePlatformMatcher
{
    private const int MinFuzzyTitleScore = 70;
    private static readonly TimeSpan DurationTolerance = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CrossPlatformDurationTolerance = TimeSpan.FromMinutes(5);

    private readonly IReadOnlyList<IReleaseMatchStrategy> _releaseMatchStrategies = releaseMatchStrategies.ToList();
    private readonly CompareInfo _compareInfo = CultureInfo.InvariantCulture.CompareInfo;

    public bool IsMatch(
        Episode existingEpisode,
        Episode incomingEpisode,
        Regex? episodeMatchRegex,
        Podcast podcast,
        IReadOnlyList<Episode> existingEpisodes)
    {
        if (EpisodeIdentityExtensions.SpotifyEpisodesMatch(existingEpisode, incomingEpisode))
        {
            return true;
        }

        if (EpisodeIdentityExtensions.IncomingPlatformIdOwnedByAnotherEpisode(
                existingEpisode,
                incomingEpisode,
                existingEpisodes))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(incomingEpisode.SpotifyId))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(incomingEpisode.YouTubeId))
        {
            return existingEpisode.YouTubeId == incomingEpisode.YouTubeId;
        }

        if (existingEpisode.AppleId.HasValue && incomingEpisode.AppleId.HasValue)
        {
            return existingEpisode.AppleId.Value == incomingEpisode.AppleId.Value;
        }

        return MatchesByTitleHeuristics(existingEpisode, incomingEpisode, episodeMatchRegex, podcast);
    }

    private bool MatchesByTitleHeuristics(
        Episode existingEpisode,
        Episode incomingEpisode,
        Regex? episodeMatchRegex,
        Podcast podcast)
    {
        if (episodeMatchRegex == null)
        {
            if (incomingEpisode.Title == existingEpisode.Title)
            {
                return true;
            }

            return MatchesByDefaultHeuristics(existingEpisode, incomingEpisode, podcast);
        }

        var incomingMatch = episodeMatchRegex.Match(incomingEpisode.Title);
        var existingMatch = episodeMatchRegex.Match(existingEpisode.Title);

        if (incomingMatch.Groups["episodematch"].Success &&
            existingMatch.Groups["episodematch"].Success)
        {
            return incomingMatch.Groups["episodematch"].Value ==
                   existingMatch.Groups["episodematch"].Value;
        }

        if (incomingMatch.Groups["title"].Success && existingMatch.Groups["title"].Success)
        {
            var incomingTitle = incomingMatch.Groups["title"].Value;
            var existingTitle = existingMatch.Groups["title"].Value;
            if (incomingTitle == existingTitle)
            {
                return true;
            }

            if (_compareInfo.Compare(incomingTitle, existingTitle, CompareOptions.IgnoreSymbols) == 0)
            {
                return true;
            }
        }

        return MatchesByDefaultHeuristics(existingEpisode, incomingEpisode, podcast);
    }

    private bool MatchesByDefaultHeuristics(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        if (MatchesByFuzzyTitleAndDuration(existingEpisode, incomingEpisode, podcast))
        {
            return true;
        }

        return MatchesByReleaseAndDuration(existingEpisode, incomingEpisode, podcast);
    }

    private static bool MatchesByFuzzyTitleAndDuration(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        if (!FuzzyMatcher.IsMatch(existingEpisode.Title, incomingEpisode, e => e.Title, MinFuzzyTitleScore))
        {
            return false;
        }

        return DurationsMatch(existingEpisode, incomingEpisode, podcast);
    }

    private bool MatchesByReleaseAndDuration(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        if (!EpisodesReleaseMatch(existingEpisode, incomingEpisode, podcast))
        {
            return false;
        }

        if (!DurationsMatch(existingEpisode, incomingEpisode, podcast))
        {
            return false;
        }

        // YouTube↔audio pairs: compose release/duration/title scores (same model for +/- delay).
        if (HasCrossPlatformYouTubeAudioPair(existingEpisode, incomingEpisode))
        {
            return CrossPlatformMatchScorer.MeetsMatchThreshold(
                existingEpisode,
                incomingEpisode,
                podcast);
        }

        if (podcast.YouTubePublishingDelay().Ticks < 0)
        {
            return FuzzyMatcher.IsMatch(existingEpisode.Title, incomingEpisode, e => e.Title, MinFuzzyTitleScore);
        }

        return true;
    }

    private bool EpisodesReleaseMatch(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        var context = new ReleaseMatchContext(podcast, existingEpisode, incomingEpisode);

        foreach (var strategy in _releaseMatchStrategies)
        {
            var result = strategy.Evaluate(context);
            if (result.HasValue)
            {
                return result.Value;
            }
        }

        return false;
    }

    private static bool DurationsMatch(Episode existingEpisode, Episode incomingEpisode, Podcast podcast)
    {
        var tolerance = GetDurationTolerance(existingEpisode, incomingEpisode, podcast);
        return Math.Abs((existingEpisode.Length - incomingEpisode.Length).Ticks) < tolerance.Ticks;
    }

    private static TimeSpan GetDurationTolerance(
        Episode existingEpisode,
        Episode incomingEpisode,
        Podcast podcast)
    {
        if (HasCrossPlatformYouTubeAudioPair(existingEpisode, incomingEpisode))
        {
            return CrossPlatformDurationTolerance;
        }

        return DurationTolerance;
    }

    private static bool HasCrossPlatformYouTubeAudioPair(Episode existingEpisode, Episode incomingEpisode)
    {
        return IsYouTubeToMissingAudioPlatformPair(existingEpisode, incomingEpisode) ||
               IsYouTubeToMissingAudioPlatformPair(incomingEpisode, existingEpisode);
    }

    /// <summary>
    /// YouTube-authority side paired with an audio-only side that supplies a platform the YouTube
    /// side still lacks. YouTube may already have Apple when Spotify arrives (or vice versa).
    /// </summary>
    private static bool IsYouTubeToMissingAudioPlatformPair(Episode youTubeSide, Episode audioSide)
    {
        if (!youTubeSide.HasYouTubeIdentity() || audioSide.HasYouTubeIdentity())
        {
            return false;
        }

        var bringsSpotify = audioSide.HasSpotifyIdentity() && !youTubeSide.HasSpotifyIdentity();
        var bringsApple = audioSide.HasAppleIdentity() && !youTubeSide.HasAppleIdentity();
        return bringsSpotify || bringsApple;
    }
}
