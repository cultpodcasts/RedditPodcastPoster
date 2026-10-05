using System.Net;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

public class EpisodeHelper : IEpisodeHelper
{
    private const int MinFuzzyTitleMatch = 95;

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
        if (hasMatchingUrl)
        {
            return true;
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

        var episodeTitle = WebUtility.HtmlDecode(episode.Title.Trim());
        if (!TryResolvedTitle(categorisedItem, out var resolvedTitle))
        {
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

        return false;
    }

    private static bool TryResolvedTitle(CategorisedItem categorisedItem, out string resolvedTitle)
    {
        string? raw = null;
        if (categorisedItem is { Authority: Service.Apple, ResolvedAppleItem: not null })
        {
            raw = categorisedItem.ResolvedAppleItem.EpisodeTitle;
        }
        else if (categorisedItem is { Authority: Service.Spotify, ResolvedSpotifyItem: not null })
        {
            raw = categorisedItem.ResolvedSpotifyItem.EpisodeTitle;
        }
        else if (categorisedItem is { Authority: Service.YouTube, ResolvedYouTubeItem: not null })
        {
            raw = categorisedItem.ResolvedYouTubeItem.EpisodeTitle;
        }
        else if (categorisedItem.Authority == Service.Other)
        {
            raw = categorisedItem.ResolvedNonPodcastServiceItem?.Title;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            resolvedTitle = string.Empty;
            return false;
        }

        resolvedTitle = WebUtility.HtmlDecode(raw.Trim());
        return true;
    }

    /// <summary>
    /// <see langword="true"/> same streaming-service URL; <see langword="false"/> same key,
    /// different URL (conflict — do not title-match); <see langword="null"/> no keyed URL to compare.
    /// </summary>
    private static bool? ClassifyResolvedNonPodcastUrl(Episode episode, ResolvedNonPodcastServiceItem? item)
    {
        if (item?.Url is null)
        {
            return null;
        }

        var existing = EpisodeServicePresence.TryGetUrl(episode, item.StreamingService);
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
