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
        // BBC Sounds / other streaming has no Spotify/Apple/YouTube resolved item, so the
        // three-platform check is vacuously true. Still match by title or existing service URL.
        if (alreadyCategorised && categorisedItem.Authority != Service.Other)
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

        if (MatchesResolvedNonPodcastUrl(episode, categorisedItem.ResolvedNonPodcastServiceItem))
        {
            return true;
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

    private static bool MatchesResolvedNonPodcastUrl(Episode episode, ResolvedNonPodcastServiceItem? item)
    {
        if (item?.Url is null || episode.Services is not { Count: > 0 })
        {
            return false;
        }

        foreach (var link in episode.Services.Values)
        {
            if (link.Url is null)
            {
                continue;
            }

            if (Uri.Compare(
                    link.Url,
                    item.Url,
                    UriComponents.Scheme | UriComponents.Host | UriComponents.Path,
                    UriFormat.Unescaped,
                    StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
        }

        return false;
    }
}
