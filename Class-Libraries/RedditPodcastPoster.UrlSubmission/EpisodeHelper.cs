using System.Net;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class EpisodeHelper : IEpisodeHelper
{
    private const int MinFuzzyTitleMatch = 95;

    public bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem)
    {
        var spotifyResolved = (categorisedItem.ResolvedSpotifyItem != null &&
                               !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                               episode.SpotifyId != categorisedItem.ResolvedSpotifyItem.EpisodeId) ||
                              categorisedItem.ResolvedSpotifyItem == null;
        var appleResolved = (categorisedItem.ResolvedAppleItem != null && episode.AppleId != null &&
                             episode.AppleId != categorisedItem.ResolvedAppleItem.EpisodeId) ||
                            categorisedItem.ResolvedAppleItem == null;
        var youTubeResolved = (categorisedItem.ResolvedYouTubeItem != null &&
                               !string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                               episode.YouTubeId != categorisedItem.ResolvedYouTubeItem.EpisodeId) ||
                              categorisedItem.ResolvedYouTubeItem == null;
        var alreadyCategorised =
            spotifyResolved &&
            appleResolved &&
            youTubeResolved;
        if (alreadyCategorised)
        {
            return false;
        }

        var matchingSpotify = categorisedItem.ResolvedSpotifyItem != null &&
                              !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                              episode.SpotifyId == categorisedItem.ResolvedSpotifyItem.EpisodeId;
        var matchingApple = categorisedItem.ResolvedAppleItem != null && episode.AppleId != null &&
                            episode.AppleId == categorisedItem.ResolvedAppleItem.EpisodeId;
        var matchingYouTube = categorisedItem.ResolvedYouTubeItem != null &&
                              !string.IsNullOrWhiteSpace(episode.YouTubeId) &&
                              episode.YouTubeId == categorisedItem.ResolvedYouTubeItem.EpisodeId;
        var hasMatchingUrl =
            matchingSpotify ||
            matchingApple ||
            matchingYouTube;
        if (hasMatchingUrl)
        {
            return true;
        }

        var episodeTitle = WebUtility.HtmlDecode(episode.Title.Trim());
        string resolvedTitle;
        if (categorisedItem is {Authority: Service.Apple, ResolvedAppleItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedAppleItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is {Authority: Service.Spotify, ResolvedSpotifyItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedSpotifyItem.EpisodeTitle.Trim());
        }
        else if (categorisedItem is {Authority: Service.YouTube, ResolvedYouTubeItem: not null})
        {
            resolvedTitle = WebUtility.HtmlDecode(categorisedItem.ResolvedYouTubeItem.EpisodeTitle.Trim());
        }
        else
        {
            return false;
        }

        if (resolvedTitle == episodeTitle ||
            resolvedTitle.Contains(episodeTitle) ||
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