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
using RedditPodcastPoster.Models.Podcasts;

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

        if (episodeChangeRequest.HashTag != null)
        {
            episode.HashTag = string.IsNullOrWhiteSpace(episodeChangeRequest.HashTag)
                ? null
                : episodeChangeRequest.HashTag.Trim();
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

        if (episodeChangeRequest.UnBluesky == true && episode.BlueskyPosted)
        {
            // Do not clear BlueskyPost here — EpisodeUpdateService deletes first, then clears only on success.
            changeState.UnBlueskyPost = true;
        }

        if (episodeChangeRequest.Subjects != null && episode.ApplyUserSubjects(episodeChangeRequest.Subjects))
        {
            changeState.UpdatedSubjects = true;
        }

        if (episodeChangeRequest.Urls?.Spotify != null)
        {
            if (episodeChangeRequest.Urls.Spotify.ToString() == string.Empty)
            {
                EpisodeServicePresence.SetSpotifyIdentity(episode, null);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
            }
            else
            {
                if (SpotifyPodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Spotify))
                {
                    var spotifyId = SpotifyIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Spotify);
                    if (!string.IsNullOrWhiteSpace(spotifyId))
                    {
                        var cleaned = episodeChangeRequest.Urls.Spotify.CleanSpotifyUrl();
                        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
                        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, cleaned, null);
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
                EpisodeServicePresence.SetAppleIdentity(episode, null);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, null, null);
            }
            else
            {
                if (ApplePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.Apple))
                {
                    var appleId = AppleIdResolver.GetEpisodeId(episodeChangeRequest.Urls.Apple);
                    if (appleId != null)
                    {
                        var cleaned = episodeChangeRequest.Urls.Apple.CleanAppleUrl();
                        EpisodeServicePresence.SetAppleIdentity(episode, appleId);
                        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, cleaned, null);
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
                EpisodeServicePresence.SetYouTubeIdentity(episode, null);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, null, null);
            }
            else
            {
                if (YouTubePodcastServiceMatcher.IsMatch(episodeChangeRequest.Urls.YouTube))
                {
                    var youTubeId = YouTubeIdResolver.Extract(episodeChangeRequest.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        var youTubeUrl = SearchResultExtensions.ToYouTubeUrl(youTubeId);
                        EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeId);
                        EpisodeServicePresence.Upsert(episode, ServiceKeys.YouTube, youTubeUrl, null);
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
                EpisodeServicePresence.Upsert(episode, ServiceKeys.BbcIplayer, null, null);
                EpisodeServicePresence.Upsert(episode, ServiceKeys.BbcSounds, null, null);
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesBBC(episodeChangeRequest.Urls.BBC))
                {
                    var bbcKey = ServiceCatalog.TryResolveKey(episodeChangeRequest.Urls.BBC) ?? ServiceKeys.BbcSounds;
                    EpisodeServicePresence.Upsert(episode, bbcKey, episodeChangeRequest.Urls.BBC, null);
                    changeState.UpdateBBCImage = true;
                }
            }
        }

        if (episodeChangeRequest.Urls?.InternetArchive != null)
        {
            if (episodeChangeRequest.Urls.InternetArchive.ToString() == string.Empty)
            {
                EpisodeServicePresence.Upsert(episode, ServiceKeys.InternetArchive, null, null);
            }
            else
            {
                if (NonPodcastServiceMatcher.MatchesInternetArchive(episodeChangeRequest.Urls.InternetArchive))
                {
                    EpisodeServicePresence.Upsert(
                        episode,
                        ServiceKeys.InternetArchive,
                        episodeChangeRequest.Urls.InternetArchive,
                        null);
                }
            }
        }

        if (episodeChangeRequest.Images?.Spotify != null)
        {
            var image = episodeChangeRequest.Images.Spotify.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Spotify;
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Spotify, image);
        }

        if (episodeChangeRequest.Images?.Apple != null)
        {
            var image = episodeChangeRequest.Images.Apple.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Apple;
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Apple, image);
        }

        if (episodeChangeRequest.Images?.YouTube != null)
        {
            var image = episodeChangeRequest.Images.YouTube.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.YouTube;
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, image);
        }

        if (episodeChangeRequest.Images?.Other != null)
        {
            var image = episodeChangeRequest.Images.Other.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Other;
            ApplyOtherImage(episode, image);
        }

        if (episodeChangeRequest.Services != null)
        {
            foreach (var pair in episodeChangeRequest.Services)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                var url = pair.Value?.Url;
                var image = pair.Value?.Image;
                if (url?.ToString() == string.Empty)
                {
                    url = null;
                }

                if (image?.ToString() == string.Empty)
                {
                    image = null;
                }

                if (url is null && image is null)
                {
                    EpisodeServicePresence.Upsert(episode, pair.Key, null, null);
                    ClearKnownServiceId(episode, pair.Key);
                    continue;
                }

                var key = pair.Key;
                if (url is not null)
                {
                    key = ServiceCatalog.TryResolveKey(url) ?? pair.Key;
                }

                EpisodeServicePresence.Upsert(episode, key, url, image);
                ApplyKnownServiceId(episode, key, url, changeState);
            }
        }
        else
        {
            EpisodeServicePresence.NormalizeCatalog(episode);
        }

        EpisodeServicePresence.SyncIds(episode);

        if (episodeChangeRequest.Language != null)
        {
            episode.Language = NormaliseEpisodeLanguage(episodeChangeRequest.Language);
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

    private static void ApplyOtherImage(Episode episode, Uri? image)
    {
        foreach (var key in ServiceCatalog.ImageCoalesceOrder)
        {
            if (key is ServiceKeys.YouTube or ServiceKeys.Spotify or ServiceKeys.Apple)
            {
                continue;
            }

            if (EpisodeServicePresence.HasUrl(episode, key) ||
                EpisodeServicePresence.TryGetImage(episode, key) is not null)
            {
                EpisodeServicePresence.SetCatalogImage(episode, key, image);
                return;
            }
        }

        if (image is not null)
        {
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.BbcIplayer, image);
        }
    }

    /// <summary>
    /// Empty and English codes are stored as null (product English/default).
    /// </summary>
    internal static string? NormaliseEpisodeLanguage(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var trimmed = language.Trim();
        var lower = trimmed.ToLowerInvariant().Replace('_', '-');
        if (lower is "en" || lower.StartsWith("en-", StringComparison.Ordinal))
        {
            return null;
        }

        return trimmed;
    }

    private static void ApplyKnownServiceId(
        Episode episode,
        string key,
        Uri? url,
        EpisodeChangeState changeState)
    {
        if (url is null)
        {
            return;
        }

        if (key == ServiceKeys.Spotify && SpotifyPodcastServiceMatcher.IsMatch(url))
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(url);
            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyId);
                changeState.UpdateSpotifyImage = true;
            }
        }
        else if (key == ServiceKeys.Apple && ApplePodcastServiceMatcher.IsMatch(url))
        {
            var appleId = AppleIdResolver.GetEpisodeId(url);
            if (appleId != null)
            {
                EpisodeServicePresence.SetAppleIdentity(episode, appleId);
                changeState.UpdateAppleImage = true;
            }
        }
        else if (key == ServiceKeys.YouTube && YouTubePodcastServiceMatcher.IsMatch(url))
        {
            var youTubeId = YouTubeIdResolver.Extract(url);
            if (!string.IsNullOrWhiteSpace(youTubeId))
            {
                EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeId);
                changeState.UpdateYouTubeImage = true;
            }
        }
    }

    private static void ClearKnownServiceId(Episode episode, string key)
    {
        if (key == ServiceKeys.Spotify)
        {
            EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        }
        else if (key == ServiceKeys.Apple)
        {
            EpisodeServicePresence.SetAppleIdentity(episode, null);
        }
        else if (key == ServiceKeys.YouTube)
        {
            EpisodeServicePresence.SetYouTubeIdentity(episode, null);
        }
    }
}
