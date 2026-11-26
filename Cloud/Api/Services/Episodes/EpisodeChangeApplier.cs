using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.BBC.Matching;
using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.InternetArchive.Matching;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Text.HashTags;

namespace Api.Services.Episodes;

public class EpisodeChangeApplier(ILogger<EpisodeChangeApplier> logger)
{
    private readonly DateTime _pastWeek = DateTime.UtcNow.AddDays(-7);

    public EpisodeChangeState Apply(Episode episode, EpisodeChangeRequest episodeChangeRequest)
    {
        var inPastWeek = episode.ReleaseUtc > _pastWeek;
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
            episode.HashTag = HashtagText.FilterPermittedStoredHashTagField(episodeChangeRequest.HashTag);
        }

        if (episodeChangeRequest.Release != null)
        {
            episode.ReleaseUtc = episodeChangeRequest.Release.Value;
            inPastWeek |= episode.ReleaseUtc > _pastWeek;
        }

        if (episodeChangeRequest.Explicit != null)
        {
            episode.Explicit = episodeChangeRequest.Explicit.Value;
        }

        // #1484: before the posting flags below, so a curator's explicit flag in the same change still wins.
        switch (episodeChangeRequest.ApprovalDecision)
        {
            case ApprovalDecision.Approve:
                EpisodeServiceLinks.Approve(episode);
                break;
            case ApprovalDecision.Reject:
                EpisodeServiceLinks.Reject(episode);
                break;
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
                // Do not clear Tweeted here — EpisodeUpdateService deletes first, then clears only when the tweet
                // is gone (#1204). Clearing first let a failed delete be re-posted by the hourly job.
                changeState.UnTweet = true;
            }
            else
            {
                episode.OldTweeted = episodeChangeRequest.Tweeted.Value ? true : null;
            }
        }

        if (episodeChangeRequest.BlueskyPosted != null)
        {
            if (!episodeChangeRequest.BlueskyPosted.Value && episode.BlueskyPosted)
            {
                // Do not clear BlueskyPost here — EpisodeUpdateService deletes first, then clears only on success.
                changeState.UnBlueskyPost = true;
            }
            else
            {
                // Curator/manual mark: set legacy flag without a network post AT URI.
                episode.OldBlueskyPosted = episodeChangeRequest.BlueskyPosted.Value ? true : null;
            }
        }
        else if (episodeChangeRequest.UnBluesky == true && episode.BlueskyPosted)
        {
            // Backward-compat alias for blueskyPosted: false — delete first, clear only on success.
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
                EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.BbcIplayer), null, null);
                EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds), null, null);
            }
            else
            {
                if (BBCUrlMatcher.IsSubmitUrl(episodeChangeRequest.Urls.BBC))
                {
                    var bbcKey = StreamingServiceCatalog.TryResolveKey(episodeChangeRequest.Urls.BBC) ?? StreamingServiceWire.ToKey(StreamingService.BbcSounds);
                    EpisodeServicePresence.Upsert(episode, bbcKey, episodeChangeRequest.Urls.BBC, null);
                    changeState.UpdateBBCImage = true;
                }
            }
        }

        if (episodeChangeRequest.Urls?.InternetArchive != null)
        {
            if (episodeChangeRequest.Urls.InternetArchive.ToString() == string.Empty)
            {
                EpisodeServicePresence.Upsert(episode, StreamingServiceWire.ToKey(StreamingService.InternetArchive), null, null);
            }
            else
            {
                if (InternetArchiveUrlMatcher.IsSubmitUrl(episodeChangeRequest.Urls.InternetArchive))
                {
                    EpisodeServicePresence.Upsert(
                        episode,
                        StreamingServiceWire.ToKey(StreamingService.InternetArchive),
                        episodeChangeRequest.Urls.InternetArchive,
                        null);
                }
            }
        }

        // #1500: track explicit curator image clears / sets so a deliberate removal is recorded on the episode.
        var imageBefore = EpisodeServicePresence.CoalescedImage(episode, StreamingServiceCatalog.ImageCoalesceOrder);
        var clearedImageKeys = new List<string>();
        var imageSet = false;

        if (episodeChangeRequest.Images?.Spotify != null)
        {
            var image = episodeChangeRequest.Images.Spotify.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Spotify;
            TrackImageEdit(ServiceKeys.Spotify, image, clearedImageKeys, ref imageSet);
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Spotify, image);
        }

        if (episodeChangeRequest.Images?.Apple != null)
        {
            var image = episodeChangeRequest.Images.Apple.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.Apple;
            TrackImageEdit(ServiceKeys.Apple, image, clearedImageKeys, ref imageSet);
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Apple, image);
        }

        if (episodeChangeRequest.Images?.YouTube != null)
        {
            var image = episodeChangeRequest.Images.YouTube.ToString() == string.Empty
                ? null
                : episodeChangeRequest.Images.YouTube;
            TrackImageEdit(ServiceKeys.YouTube, image, clearedImageKeys, ref imageSet);
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, image);
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
                    key = StreamingServiceCatalog.TryResolveKey(url) ?? pair.Key;
                }

                if (pair.Value?.Image is not null)
                {
                    // #1500: an explicit "" image on a link that stays is a curator clearing that service's art.
                    // Deleting the whole link ("" url and image) is handled above and is not an image removal.
                    TrackImageEdit(key, image, clearedImageKeys, ref imageSet);
                }

                EpisodeServicePresence.Upsert(episode, key, url, image);
                if (pair.Value?.Image is not null && image is null)
                {
                    // #1500: an explicit "" image on a kept link clears that service's art. Upsert keeps an image it
                    // is not given, so clear it here.
                    EpisodeServicePresence.SetCatalogImage(episode, key, null);
                }

                ApplyKnownServiceId(episode, key, url, changeState);
            }
        }
        else
        {
            EpisodeServicePresence.NormalizeCatalog(episode);
        }

        EpisodeServicePresence.SyncIds(episode);
        ApplyImageRemoved(episode, imageBefore, clearedImageKeys, imageSet);

        if (episodeChangeRequest.Language != null)
        {
            episode.Language = NormaliseEpisodeLanguage(episodeChangeRequest.Language);
        }

        if (episodeChangeRequest.HasHomepageAffectingChange && inPastWeek)
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

        YouTubeAlternateChangeApplier.Apply(
            episode, episodeChangeRequest.AdditionalYouTube, episodeChangeRequest.PromoteYouTubeId);

        return changeState;
    }

    /// <summary>
    /// The primary YouTube id after this change's <c>services</c> or <c>urls.youtube</c> is applied.
    /// Services win when both are sent, matching <see cref="Apply"/>.
    /// </summary>
    internal static string? ResultingYouTubePrimary(Episode episode, EpisodeChangeRequest request)
    {
        if (request.Services is not null)
        {
            return YouTubeAlternateChangeApplier.ResultingPrimaryYouTubeId(episode, request.Services);
        }

        if (request.Urls?.YouTube is { } url)
        {
            if (url.ToString() == string.Empty)
            {
                return null;
            }

            if (YouTubePodcastServiceMatcher.IsMatch(url))
            {
                var id = YouTubeIdResolver.Extract(url);
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
        }

        return YouTubeAlternateChangeApplier.CurrentPrimaryYouTubeId(episode);
    }

    private static void TrackImageEdit(string key, Uri? image, List<string> clearedImageKeys, ref bool imageSet)
    {
        if (image is null)
        {
            clearedImageKeys.Add(key);
        }
        else
        {
            imageSet = true;
        }
    }

    /// <summary>
    /// #1500: a curator setting any image clears <see cref="Episode.ImageRemoved"/>. A curator clearing the image of a
    /// link that stays on the episode, when that leaves the episode with no image at all (it had one before), records
    /// the removal, so search, D1, the homepage, share cards and social posts show no art rather than falling back to
    /// the YouTube thumbnail. Clearing one image while another remains, deleting a whole link, or clearing an image
    /// that was already empty changes nothing. Edits that touch no image leave the flag as it is.
    /// </summary>
    internal static void ApplyImageRemoved(
        Episode episode,
        Uri? imageBefore,
        IReadOnlyCollection<string> clearedImageKeys,
        bool imageSet)
    {
        if (imageSet)
        {
            episode.ImageRemoved = null;
            return;
        }

        if (imageBefore is null || clearedImageKeys.Count == 0)
        {
            return;
        }

        if (EpisodeServicePresence.CoalescedImage(episode, StreamingServiceCatalog.ImageCoalesceOrder) is not null)
        {
            return;
        }

        if (clearedImageKeys.Any(key => EpisodeServicePresence.TryGetUrl(episode, key) is not null))
        {
            episode.ImageRemoved = true;
        }
    }

    /// <summary>
    /// #1484: why a change cannot be applied to <paramref name="episode"/>'s approval status, or null. An episode held
    /// for approval (awaiting or rejected) is made public only by approving it, never by clearing <c>removed</c>; only
    /// a held episode can be rejected (null means approved, like any ordinary episode, which is hidden with
    /// <c>removed</c>); and a decision cannot be combined with the opposite <c>removed</c>. Approving an episode that is
    /// already approved is accepted and changes nothing.
    /// </summary>
    internal static string? SplitOffReviewError(Episode episode, EpisodeChangeRequest request)
    {
        switch (request.ApprovalDecision)
        {
            case null:
                return request.Removed == false && episode.IsHeldForApproval()
                    ? "This split-off is awaiting approval or rejected: approve it to make it public."
                    : null;
            case ApprovalDecision.Reject when !episode.IsHeldForApproval():
                return "Only an episode awaiting approval can be rejected.";
            case ApprovalDecision.Approve when request.Removed == true:
                return "An approved split-off cannot also be removed in the same change.";
            case ApprovalDecision.Reject when request.Removed == false:
                return "A rejected split-off stays removed.";
            default:
                return null;
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
