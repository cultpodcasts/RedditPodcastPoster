using Api.Models;
using Microsoft.Extensions.Logging;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace Api.Services.Episodes;

public class EpisodeChangeApplier(ILogger<EpisodeChangeApplier> logger)
{
    private readonly DateTime _pastWeek = DateTime.UtcNow.AddDays(-7);

    public EpisodeChangeState Apply(Episode episode, EpisodeChangeRequest episodeChangeRequest)
    {
        var inPastWeek = episode.Release > _pastWeek;
        var changeState = new EpisodeChangeState();
        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Title))
        {
            episode.Title = episodeChangeRequest.Title;
        }

        if (episodeChangeRequest.Description != null)
        {
            episode.Description = episodeChangeRequest.Description;
        }

        if (!string.IsNullOrWhiteSpace(episodeChangeRequest.Duration))
        {
            episode.Length = TimeSpan.Parse(episodeChangeRequest.Duration);
        }

        if (episodeChangeRequest.SearchTerms != null)
        {
            episode.SearchTerms = episodeChangeRequest.SearchTerms;
        }

        if (episodeChangeRequest.Release != null)
        {
            episode.Release = episodeChangeRequest.Release.Value;
            inPastWeek |= episode.Release > _pastWeek;
        }

        if (episodeChangeRequest.Explicit != null)
        {
            episode.Explicit = episodeChangeRequest.Explicit.Value;
        }

        if (episodeChangeRequest.Ignored != null)
        {
            episode.Ignored = episodeChangeRequest.Ignored.Value;
        }

        if (episodeChangeRequest.Posted != null)
        {
            if (!episodeChangeRequest.Posted.Value && episode.Posted)
            {
                changeState.UnPost = true;
            }

            episode.Posted = episodeChangeRequest.Posted.Value;
        }

        if (episodeChangeRequest.Removed != null)
        {
            episode.Removed = episodeChangeRequest.Removed.Value;
        }

        if (episodeChangeRequest.Tweeted != null)
        {
            if (!episodeChangeRequest.Tweeted.Value && episode.Tweeted)
            {
                changeState.UnTweet = true;
            }

            episode.Tweeted = episodeChangeRequest.Tweeted.Value;
        }

        if (episodeChangeRequest.BlueskyPosted != null)
        {
            if (!episodeChangeRequest.BlueskyPosted.Value && episode.BlueskyPosted.HasValue &&
                episode.BlueskyPosted.Value)
            {
                changeState.UnBlueskyPost = true;
            }

            episode.BlueskyPosted =
                episodeChangeRequest.BlueskyPosted.HasValue && episodeChangeRequest.BlueskyPosted.Value ? true : null;
        }

        if (episodeChangeRequest.Subjects != null && episode.ApplyUserSubjects(episodeChangeRequest.Subjects))
        {
            changeState.UpdatedSubjects = true;
        }

        if (episodeChangeRequest.Urls?.Spotify != null)
        {
            if (episodeChangeRequest.Urls.Spotify.ToString() == string.Empty)
            {
                episode.SpotifyId = string.Empty;
                episode.Urls.Spotify = null;
                if (episode.Images != null)
                {
                    episode.Images.Spotify = null;
                }
            }
            else
            {
                if (SpotifyPodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Spotify))
                {
                    var spotifyId = SpotifyIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Spotify);
                    if (!string.IsNullOrWhiteSpace(spotifyId))
                    {
                        episode.SpotifyId = spotifyId;
                        episode.Urls.Spotify = episodeChangeRequest.Urls.Spotify.CleanSpotifyUrl();
                        changeState.UpdateSpotifyImage = true;
                    }
                }
                else
                {
                    logger.LogError("Invalid spotify-url: '{spotifyUrl}'.", episodeChangeRequest.Urls.Spotify);
                }
            }
        }

        if (episodeChangeRequest.Urls?.Apple != null)
        {
            if (episodeChangeRequest.Urls.Apple.ToString() == string.Empty)
            {
                episode.AppleId = null;
                episode.Urls.Apple = null;
                if (episode.Images != null)
                {
                    episode.Images.Apple = null;
                }
            }
            else
            {
                if (ApplePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Apple))
                {
                    var appleId = AppleIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Apple);
                    if (appleId != null)
                    {
                        episode.AppleId = appleId;
                        episode.Urls.Apple = episodeChangeRequest.Urls.Apple.CleanAppleUrl();
                        changeState.UpdateAppleImage = true;
                    }
                }
                else
                {
                    logger.LogError("Invalid apple-url: '{appleUrl}'.", episodeChangeRequest.Urls.Apple);
                }
            }
        }

        if (episodeChangeRequest.Urls?.YouTube != null)
        {
            if (episodeChangeRequest.Urls.YouTube.ToString() == string.Empty)
            {
                episode.YouTubeId = string.Empty;
                episode.Urls.YouTube = null;
                if (episode.Images != null)
                {
                    episode.Images.YouTube = null;
                }
            }
            else
            {
                if (YouTubePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.YouTube))
                {
                    var youTubeId = YouTubeIdResolver.Extract(episodeChangeRequest.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        episode.YouTubeId = youTubeId;
                        episode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(youTubeId);
                        changeState.UpdateYouTubeImage = true;
                    }
                    else
                    {
                        logger.LogError("Invalid youtube-url: '{youTubeUrl}'.", episodeChangeRequest.Urls.YouTube);
                    }
                }
            }
        }

        if (episodeChangeRequest.Urls?.BBC != null)
        {
            if (episodeChangeRequest.Urls.BBC.ToString() == string.Empty)
            {
                episode.Urls.BBC = null;
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesBBC(episodeChangeRequest.Urls.BBC))
                {
                    episode.Urls.BBC = episodeChangeRequest.Urls.BBC;
                    changeState.UpdateBBCImage = true;
                }
            }
        }

        if (episodeChangeRequest.Urls?.InternetArchive != null)
        {
            if (episodeChangeRequest.Urls.InternetArchive.ToString() == string.Empty)
            {
                episode.Urls.InternetArchive = null;
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesInternetArchive(episodeChangeRequest.Urls.InternetArchive))
                {
                    episode.Urls.InternetArchive = episodeChangeRequest.Urls.InternetArchive;
                }
            }
        }

        if (episodeChangeRequest.Images?.Spotify != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.Spotify = episodeChangeRequest.Images.Spotify.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Spotify;
        }

        if (episodeChangeRequest.Images?.Apple != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.Apple = episodeChangeRequest.Images.Apple.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Apple;
        }

        if (episodeChangeRequest.Images?.YouTube != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.YouTube = episodeChangeRequest.Images.YouTube.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.YouTube;
        }

        if (episodeChangeRequest.Images?.Other != null)
        {
            episode.Images ??= new EpisodeImages();
            episode.Images.Other = episodeChangeRequest.Images.Other.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Other;
        }

        if (episode.Images != null &&
            episode.Images.YouTube == null &&
            episode.Images.Spotify == null &&
            episode.Images.Apple == null &&
            episode.Images.Other == null)
        {
            episode.Images = null;
        }

        if (episodeChangeRequest.Language != null)
        {
            episode.Language =
                episodeChangeRequest.Language == string.Empty ? null : episodeChangeRequest.Language;
        }

        if (episodeChangeRequest.HasChange && inPastWeek)
        {
            changeState.PublishHomepage = true;
        }

        if (episodeChangeRequest.Guests != null)
        {
            episode.Guests = episodeChangeRequest.Guests.Length > 0
                ? episodeChangeRequest.Guests
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .ToArray()
                : null;
        }

        return changeState;
    }
}
