using System.Net;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

public class EpisodeHelper : IEpisodeHelper
{
    /// <summary>Title-only bar: no release or duration evidence, so the title has to carry the match.</summary>
    private const int MinFuzzyTitleMatch = 95;

    /// <summary>
    /// Title bar for a YouTube↔audio pair that already agrees on release and duration. Same bar as
    /// <see cref="CrossPlatformMatchScorer"/> title credit (#1445).
    /// </summary>
    public const int MinCrossPlatformFuzzyTitleMatch = 70;

    /// <summary>
    /// Floor of the duration band for a YouTube↔audio pair. Tighter than the generic 5-minute
    /// cross-platform band because this path relaxes the title bar (#1445 review).
    /// </summary>
    public static readonly TimeSpan CrossPlatformDurationToleranceFloor = TimeSpan.FromMinutes(2);

    /// <summary>Proportional duration band for a YouTube↔audio pair, as a fraction of the longer length.</summary>
    public const double CrossPlatformDurationToleranceFraction = 0.03;

    /// <summary>Greater of <see cref="CrossPlatformDurationToleranceFloor"/> and 3% of the longer length.</summary>
    public static TimeSpan CrossPlatformDurationTolerance(TimeSpan left, TimeSpan right)
    {
        var longer = left > right ? left : right;
        var proportional = TimeSpan.FromTicks((long)(longer.Ticks * CrossPlatformDurationToleranceFraction));
        return proportional > CrossPlatformDurationToleranceFloor ? proportional : CrossPlatformDurationToleranceFloor;
    }

    public bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem)
    {
        var spotifyResolved = (categorisedItem.ResolvedSpotifyItem != null &&
                               !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)) &&
                               EpisodeServicePresence.SpotifyEpisodeId(episode) !=
                               categorisedItem.ResolvedSpotifyItem.EpisodeId) ||
                              categorisedItem.ResolvedSpotifyItem == null;
        var appleResolved = (categorisedItem.ResolvedAppleItem != null &&
                             EpisodeServicePresence.AppleEpisodeId(episode) != null &&
                             EpisodeServicePresence.AppleEpisodeId(episode) !=
                             categorisedItem.ResolvedAppleItem.EpisodeId) ||
                            categorisedItem.ResolvedAppleItem == null;
        var youTubeResolved = (categorisedItem.ResolvedYouTubeItem != null &&
                               !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) &&
                               EpisodeServicePresence.YouTubeEpisodeId(episode) !=
                               categorisedItem.ResolvedYouTubeItem.EpisodeId) ||
                              categorisedItem.ResolvedYouTubeItem == null;
        var alreadyCategorised = spotifyResolved && appleResolved && youTubeResolved;
        var hasPodcastServiceItem =
            categorisedItem.ResolvedSpotifyItem != null ||
            categorisedItem.ResolvedAppleItem != null ||
            categorisedItem.ResolvedYouTubeItem != null;
        // A Sounds/IA/other submit has no Spotify/Apple/YouTube resolved item, so the
        // three-platform check is vacuously true. Only early-return when a podcast-service
        // item is present and already assigned to a different identity.
        if (hasPodcastServiceItem && alreadyCategorised)
        {
            return false;
        }

        var matchingSpotify = categorisedItem.ResolvedSpotifyItem != null &&
                              !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)) &&
                              EpisodeServicePresence.SpotifyEpisodeId(episode) ==
                              categorisedItem.ResolvedSpotifyItem.EpisodeId;
        var matchingApple = categorisedItem.ResolvedAppleItem != null &&
                            EpisodeServicePresence.AppleEpisodeId(episode) != null &&
                            EpisodeServicePresence.AppleEpisodeId(episode) ==
                            categorisedItem.ResolvedAppleItem.EpisodeId;
        var matchingYouTube = categorisedItem.ResolvedYouTubeItem != null &&
                              !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) &&
                              EpisodeServicePresence.YouTubeEpisodeId(episode) ==
                              categorisedItem.ResolvedYouTubeItem.EpisodeId;
        var hasMatchingUrl = matchingSpotify || matchingApple || matchingYouTube;
        TryResolvedAuthority(categorisedItem, out var authority);
        if (hasMatchingUrl)
        {
            var matchesAuthorityIdentity = categorisedItem.Authority switch
            {
                Service.Spotify => matchingSpotify,
                Service.Apple => matchingApple,
                Service.YouTube => matchingYouTube,
                _ => false
            };

            // The submitted service's own id is definitive. An id that only came from a secondary
            // (MatchOtherServices) resolve can point at a different stored episode, so it must also
            // look like the submitted episode before it is accepted (#1445).
            if (matchesAuthorityIdentity || authority is null || authority.Service == Service.Other ||
                IsPlausiblySameEpisode(episode, authority))
            {
                return true;
            }

            return false;
        }

        var nonPodcastUrlMatch =
            ClassifyResolvedNonPodcastUrl(episode, categorisedItem.ResolvedNonPodcastServiceItem);
        if (nonPodcastUrlMatch == true)
        {
            return true;
        }

        if (nonPodcastUrlMatch == false)
        {
            return false;
        }

        if (authority is null)
        {
            return false;
        }

        var episodeTitle = NormaliseTitle(episode.Title);
        var resolvedTitle = authority.Title;
        if (EpisodeTitlePartMarkers.Conflict(resolvedTitle, episodeTitle))
        {
            // pt.I vs pt.II (or ep 3 vs ep 4) are different episodes however close the stems are.
            return false;
        }

        if (resolvedTitle == episodeTitle || resolvedTitle.Contains(episodeTitle) ||
            episodeTitle.Contains(resolvedTitle))
        {
            return true;
        }

        if (FuzzyMatcher.IsMatch(resolvedTitle, episodeTitle, e => e, MinFuzzyTitleMatch))
        {
            return true;
        }

        return IsCrossPlatformMatch(episode, authority, categorisedItem.MatchingPodcast);
    }

    public IReadOnlyList<string> ConflictingServiceKeys(Episode episode, CategorisedItem categorisedItem)
    {
        var authorityKey = categorisedItem.Authority switch
        {
            Service.Spotify => ServiceKeys.Spotify,
            Service.Apple => ServiceKeys.Apple,
            _ => null
        };
        if (authorityKey is null)
        {
            return [];
        }

        var conflicting = new List<string>();
        if (categorisedItem.ResolvedSpotifyItem is { } spotify &&
            EpisodeServicePresence.SpotifyEpisodeId(episode) is { } storedSpotifyId &&
            storedSpotifyId != spotify.EpisodeId)
        {
            conflicting.Add(ServiceKeys.Spotify);
        }

        if (categorisedItem.ResolvedAppleItem is { } apple &&
            EpisodeServicePresence.AppleEpisodeId(episode) is { } storedAppleId &&
            storedAppleId != apple.EpisodeId)
        {
            conflicting.Add(ServiceKeys.Apple);
        }

        if (!conflicting.Contains(authorityKey))
        {
            return [];
        }

        // The conflict is "would match but for the stored links": the same title, release and duration rules as
        // a normal submit decide, so a conflict prompt never fires for an episode a submit would not attach to.
        return IsMatchingEpisode(WithoutServiceLinks(episode, conflicting), categorisedItem) ? conflicting : [];
    }

    /// <summary>
    /// Detached copy of the fields <see cref="IsMatchingEpisode"/> reads, minus <paramref name="serviceKeys"/>'s
    /// ids and links. The stored episode is not changed.
    /// </summary>
    private static Episode WithoutServiceLinks(Episode episode, IReadOnlyCollection<string> serviceKeys)
    {
        var copy = new Episode
        {
            Id = episode.Id,
            PodcastId = episode.PodcastId,
            Title = episode.Title,
            Length = episode.Length,
            Ids = episode.Ids is { } ids
                ? new EpisodeIds { Spotify = ids.Spotify, Apple = ids.Apple, YouTube = ids.YouTube }
                : null,
            Services = episode.Services is { } services
                ? services
                    .Where(link => !serviceKeys.Contains(link.Key))
                    .ToDictionary(link => link.Key, link => link.Value, StringComparer.Ordinal)
                : null
        };
        copy.ReleaseUtc = episode.ReleaseUtc;
        if (serviceKeys.Contains(ServiceKeys.Spotify))
        {
            EpisodeServicePresence.SetSpotifyIdentity(copy, null);
        }

        if (serviceKeys.Contains(ServiceKeys.Apple))
        {
            EpisodeServicePresence.SetAppleIdentity(copy, null);
        }

        return copy;
    }

    /// <summary>
    /// YouTube↔audio pair (#1445): the stored episode carries the other platform's identity and is
    /// missing the submitted one, releases share a calendar day (or sit within the same-release
    /// window, or line up with the podcast's YouTube publishing delay), durations are within the
    /// cross-platform band (<see cref="CrossPlatformDurationTolerance"/>), the titles carry no conflicting
    /// part or episode number, and they clear <see cref="MinCrossPlatformFuzzyTitleMatch"/>.
    /// </summary>
    private static bool IsCrossPlatformMatch(Episode episode, ResolvedAuthority authority, Podcast? podcast)
    {
        if (!IsYouTubeAudioPair(episode, authority.Service))
        {
            return false;
        }

        return !EpisodeTitlePartMarkers.Conflict(authority.Title, NormaliseTitle(episode.Title)) &&
               ReleaseAndDurationAgree(episode, authority, podcast) &&
               FuzzyMatcher.Score(authority.Title, NormaliseTitle(episode.Title)) >=
               MinCrossPlatformFuzzyTitleMatch;
    }

    /// <summary>
    /// A secondary-id hit needs title evidence: equal, contained, or fuzzy at the cross-platform bar,
    /// with no conflicting part or episode number. Release and duration alone are not enough, because a
    /// same-day sibling (the other part, or a similar-length daily episode) would pass (#1445 review).
    /// </summary>
    private static bool IsPlausiblySameEpisode(Episode episode, ResolvedAuthority authority)
    {
        var episodeTitle = NormaliseTitle(episode.Title);
        if (EpisodeTitlePartMarkers.Conflict(authority.Title, episodeTitle))
        {
            return false;
        }

        return authority.Title == episodeTitle ||
               authority.Title.Contains(episodeTitle) ||
               episodeTitle.Contains(authority.Title) ||
               FuzzyMatcher.Score(authority.Title, episodeTitle) >= MinCrossPlatformFuzzyTitleMatch;
    }

    private static bool ReleaseAndDurationAgree(Episode episode, ResolvedAuthority authority, Podcast? podcast)
    {
        if (authority.Duration <= TimeSpan.Zero || episode.Length <= TimeSpan.Zero)
        {
            return false;
        }

        var durationDelta = (authority.Duration - episode.Length).Duration();
        if (durationDelta > CrossPlatformDurationTolerance(authority.Duration, episode.Length))
        {
            return false;
        }

        var releaseDelta = (authority.Release - episode.ReleaseUtc).Duration();
        if (releaseDelta < EpisodeReleaseTolerance.SameReleaseThreshold)
        {
            return true;
        }

        var (audioRelease, youTubeRelease) = authority.Service == Service.YouTube
            ? (episode.ReleaseUtc, authority.Release)
            : (authority.Release, episode.ReleaseUtc);
        if (EpisodeReleaseTolerance.AreCrossPlatformReleasesOnSameCalendarDay(audioRelease, youTubeRelease))
        {
            return true;
        }

        return podcast != null &&
               EpisodeReleaseTolerance.IsYouTubePublishDelayAligned(
                   audioRelease,
                   youTubeRelease,
                   podcast.YouTubePublishingDelay());
    }

    private static bool IsYouTubeAudioPair(Episode episode, Service authorityService)
    {
        var hasYouTube = !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)) ||
                         EpisodeServicePresence.HasUrl(episode, ServiceKeys.YouTube);
        var hasSpotify = !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)) ||
                         EpisodeServicePresence.HasUrl(episode, ServiceKeys.Spotify);
        var hasApple = EpisodeServicePresence.AppleEpisodeId(episode) is not null and not 0 ||
                       EpisodeServicePresence.HasUrl(episode, ServiceKeys.Apple);
        return authorityService switch
        {
            Service.Spotify => hasYouTube && !hasSpotify,
            Service.Apple => hasYouTube && !hasApple,
            Service.YouTube => !hasYouTube && (hasSpotify || hasApple),
            _ => false
        };
    }

    private sealed record ResolvedAuthority(Service Service, string Title, DateTime Release, TimeSpan Duration);

    private static bool TryResolvedAuthority(CategorisedItem categorisedItem, out ResolvedAuthority? authority)
    {
        authority = categorisedItem switch
        {
            { Authority: Service.Apple, ResolvedAppleItem: { } apple } =>
                Create(Service.Apple, apple.EpisodeTitle, apple.Release, apple.Duration),
            { Authority: Service.Spotify, ResolvedSpotifyItem: { } spotify } =>
                Create(Service.Spotify, spotify.EpisodeTitle, spotify.Release, spotify.Duration),
            { Authority: Service.YouTube, ResolvedYouTubeItem: { } youTube } =>
                Create(Service.YouTube, youTube.EpisodeTitle, youTube.Release, youTube.Duration),
            { Authority: Service.Other } =>
                Create(Service.Other, categorisedItem.ResolvedNonPodcastServiceItem?.Title, default, TimeSpan.Zero),
            _ => null
        };
        return authority != null;

        static ResolvedAuthority? Create(Service service, string? title, DateTime release, TimeSpan duration) =>
            string.IsNullOrWhiteSpace(title)
                ? null
                : new ResolvedAuthority(service, NormaliseTitle(title), release, duration);
    }

    private static string NormaliseTitle(string title) => WebUtility.HtmlDecode(title.Trim());

    /// <summary>
    /// <see langword="true"/> same streaming-service URL; <see langword="false"/> same key,
    /// different URL (conflict — do not title-match); <see langword="null"/> no keyed URL to compare.
    /// </summary>
    private static bool? ClassifyResolvedNonPodcastUrl(Episode episode, ResolvedNonPodcastServiceItem? item)
    {
        if (item?.Url is null || item.StreamingService is not { } service)
        {
            return null;
        }

        var existing = EpisodeServicePresence.TryGetUrl(episode, service);
        if (existing is null)
        {
            return null;
        }

        return Uri.Compare(
                   existing,
                   item.Url,
                   UriComponents.Scheme | UriComponents.Host | UriComponents.Path,
                   UriFormat.Unescaped,
                   StringComparison.OrdinalIgnoreCase) == 0;
    }
}
