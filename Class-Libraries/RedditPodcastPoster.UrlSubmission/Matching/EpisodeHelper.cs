using System.Net;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.Text.Matchers;

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
        if (alreadyCategorised)
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

        var episodeTitle = WebUtility.HtmlDecode(episode.Title.Trim());
        string resolvedTitle;
        if (categorisedItem is { Authority: Service.Apple, ResolvedAppleItem: not null })
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedAppleItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is { Authority: Service.Spotify, ResolvedSpotifyItem: not null })
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedSpotifyItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is { Authority: Service.YouTube, ResolvedYouTubeItem: not null })
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedYouTubeItem.EpisodeTitle.Trim());
        }
        else
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
}