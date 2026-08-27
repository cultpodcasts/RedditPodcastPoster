using Api.Models; // pragma: allowlist secret
using Microsoft.Extensions.Logging; // pragma: allowlist secret
using Episode = RedditPodcastPoster.Models.Episodes.Episode; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Apple; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Apple.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Apple.Resolvers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Spotify; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Spotify.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Extensions; // pragma: allowlist secret
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers; // pragma: allowlist secret
using RedditPodcastPoster.Models.Podcasts; // pragma: allowlist secret

namespace Api.Services.Episodes; // pragma: allowlist secret

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
                    EpisodeServicePresence.Upsert(episode, pair.Key, null, null); // pragma: allowlist secret
                    ClearKnownServiceId(episode, pair.Key);
                    continue;
                }

                var key = pair.Key;
                if (url is not null)
                {
                    key = ServiceCatalog.TryResolveKey(url) ?? pair.Key;
                }

                EpisodeServicePresence.Upsert(episode, key, url, image); // pragma: allowlist secret
                ApplyKnownServiceId(episode, key, url, changeState);
            }
        }
        else
        {
            EpisodeServicePresence.Hydrate(episode); // pragma: allowlist secret
            EpisodeServicePresence.SyncLegacy(episode); // pragma: allowlist secret
        }

        EpisodeServicePresence.SyncIds(episode); // pragma: allowlist secret

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

        if (key == ServiceKeys.Spotify && SpotifyPodcastServiceMatcher.IsMatch(url)) // pragma: allowlist secret
        {
            var spotifyId = SpotifyIdResolver.GetEpisodeId(url);
            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                episode.SpotifyId = spotifyId;
                changeState.UpdateSpotifyImage = true;
            }
        }
        else if (key == ServiceKeys.Apple && ApplePodcastServiceMatcher.IsMatch(url)) // pragma: allowlist secret
        {
            var appleId = AppleIdResolver.GetEpisodeId(url);
            if (appleId != null)
            {
                episode.AppleId = appleId;
                changeState.UpdateAppleImage = true;
            }
        }
        else if (key == ServiceKeys.YouTube && YouTubePodcastServiceMatcher.IsMatch(url)) // pragma: allowlist secret
        {
            var youTubeId = YouTubeIdResolver.Extract(url);
            if (!string.IsNullOrWhiteSpace(youTubeId))
            {
                episode.YouTubeId = youTubeId;
                changeState.UpdateYouTubeImage = true;
            }
        }
    }

    private static void ClearKnownServiceId(Episode episode, string key)
    {
        if (key == ServiceKeys.Spotify)
        {
            episode.SpotifyId = string.Empty;
        }
        else if (key == ServiceKeys.Apple)
        {
            episode.AppleId = null;
        }
        else if (key == ServiceKeys.YouTube)
        {
            episode.YouTubeId = string.Empty;
        }
    }
}
