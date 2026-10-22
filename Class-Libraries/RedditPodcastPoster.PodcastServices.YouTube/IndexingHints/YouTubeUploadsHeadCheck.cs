using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Playlist;

namespace RedditPodcastPoster.PodcastServices.YouTube.IndexingHints;

public interface IYouTubeUploadsHeadCheck
{
    /// <summary>
    /// Finds new playlist members from the channel's newest uploads instead of walking the playlist. Never returns
    /// members unless the playlist's count proves nothing else changed; any doubt is a <c>FallbackReason</c>.
    /// </summary>
    Task<YouTubeUploadsHeadResult> Check(
        IYouTubeIndexingSettings settings,
        YouTubeIndexingHints hints,
        YouTubePlaylistId playlistId,
        IReadOnlyCollection<string> knownIds,
        IndexingContext indexingContext);
}

/// <summary>
/// The uploads-head path of the YouTube indexing hints. In quota units (playlistItems.list is 1 unit per call):
/// <list type="number">
/// <item>The playlist's head with maxResults 1 for <c>pageInfo.totalResults</c> (1 unit).</item>
/// <item>The channel's uploads playlist (<c>UU</c> + channel id without <c>UC</c>), newest 50 (1 unit).</item>
/// <item>Uploads newer than the watermark (<see cref="YouTubeIndexingHintsPlanner.Watermark"/>) are the only ones that
/// can be new members. If the count is unchanged there are none (an add and a remove in the same hour is left to the
/// daily full walk). Otherwise each newer upload gets a membership check (1 unit each), and the members found must
/// account for the whole count change.</item>
/// </list>
/// Fallback reasons: <see cref="NoUploadsPlaylist"/>, <see cref="PlaylistCountFailed"/>,
/// <see cref="UploadsReadFailed"/>, <see cref="UploadsGap"/>, <see cref="FullWalkCheaper"/>,
/// <see cref="MembershipUnknown"/>, <see cref="PlaylistCountChanged"/>, <see cref="QuotaExhausted"/>.
/// </summary>
public class YouTubeUploadsHeadCheck(
    ITolerantYouTubePlaylistService playlistService,
    IYouTubePlaylistMembership playlistMembership,
    IYouTubeServiceWrapper youTubeService,
    ILogger<YouTubeUploadsHeadCheck> logger) : IYouTubeUploadsHeadCheck
{
    public const int UploadsPageSize = YouTubePlaylistService.PlaylistPageSize;

    public const string NoUploadsPlaylist = "no-uploads-playlist";
    public const string PlaylistCountFailed = "playlist-count-failed";
    public const string UploadsReadFailed = "uploads-read-failed";
    public const string UploadsGap = "uploads-gap";
    public const string FullWalkCheaper = "full-walk-cheaper";
    public const string MembershipUnknown = "membership-unknown";
    public const string PlaylistCountChanged = "playlist-count-changed";
    public const string QuotaExhausted = "quota-exhausted";

    /// <summary>
    /// The uploads playlist id for a <c>UC…</c> channel id (<c>UU…</c>), without a channels.list call. Null for any
    /// other id shape.
    /// </summary>
    public static string? UploadsPlaylistId(string? channelId) =>
        channelId is { Length: > 2 } && channelId.StartsWith("UC", StringComparison.Ordinal)
            ? "UU" + channelId[2..]
            : null;

    public async Task<YouTubeUploadsHeadResult> Check(
        IYouTubeIndexingSettings settings,
        YouTubeIndexingHints hints,
        YouTubePlaylistId playlistId,
        IReadOnlyCollection<string> knownIds,
        IndexingContext indexingContext)
    {
        var uploadsPlaylistId = UploadsPlaylistId(settings.YouTubeChannelId);
        if (uploadsPlaylistId == null)
        {
            return YouTubeUploadsHeadResult.Fallback(NoUploadsPlaylist);
        }

        var head = await playlistService.GetPlaylistHead(playlistId, 1, indexingContext);
        if (head.Items == null || head.TotalResults is not { } total)
        {
            return YouTubeUploadsHeadResult.Fallback(FailureReason(head.Failure, PlaylistCountFailed));
        }

        var uploads = await playlistService.GetPlaylistHead(
            new YouTubePlaylistId(uploadsPlaylistId, YouTubePlaylistIdSource.ChannelUploads,
                settings.YouTubeChannelId!),
            UploadsPageSize,
            indexingContext);
        if (uploads.Items == null)
        {
            return YouTubeUploadsHeadResult.Fallback(FailureReason(uploads.Failure, UploadsReadFailed));
        }

        var watermark = YouTubeIndexingHintsPlanner.Watermark(hints);
        var newer = uploads.Items
            .Select(item => (Item: item, Date: item.GetIndexingWindowDate()?.UtcDateTime))
            .Where(x => x.Date.HasValue && (watermark == null || x.Date > watermark))
            .ToList();
        if (uploads.Items.Count >= UploadsPageSize && newer.Count == uploads.Items.Count)
        {
            // Every upload on the page is newer than the watermark: older unchecked uploads may lie beyond it.
            return YouTubeUploadsHeadResult.Fallback(UploadsGap);
        }

        var newest = newer.OrderByDescending(x => x.Date).FirstOrDefault();
        var delta = total - (hints.PlaylistItemCount ?? 0);
        if (delta == 0)
        {
            return new YouTubeUploadsHeadResult([], total, newest.Date, newest.Item?.Snippet?.ResourceId?.VideoId,
                0, null);
        }

        if (delta < 0 || newer.Count < delta)
        {
            // Items were removed, or more were added than there are new uploads to account for them (an older
            // video added to the playlist).
            return YouTubeUploadsHeadResult.Fallback(PlaylistCountChanged);
        }

        var fullWalkPages = (int) Math.Ceiling(total / (double) YouTubePlaylistService.PlaylistPageSize);
        if (newer.Count >= fullWalkPages)
        {
            return YouTubeUploadsHeadResult.Fallback(FullWalkCheaper);
        }

        var known = knownIds.ToHashSet(StringComparer.Ordinal);
        var members = new List<PlaylistItem>();
        var membersFound = 0;
        var checkedCount = 0;
        foreach (var (item, _) in newer)
        {
            var videoId = item.Snippet?.ResourceId?.VideoId;
            if (string.IsNullOrWhiteSpace(videoId))
            {
                return YouTubeUploadsHeadResult.Fallback(MembershipUnknown, checkedCount);
            }

            indexingContext.CancellationToken.ThrowIfCancellationRequested();
            var isMember = await playlistMembership.Contains(youTubeService, playlistId.PlaylistId, videoId);
            checkedCount++;
            if (isMember == null)
            {
                return YouTubeUploadsHeadResult.Fallback(MembershipUnknown, checkedCount);
            }

            if (isMember.Value)
            {
                membersFound++;
                if (!known.Contains(videoId) &&
                    item.GetIndexingWindowDate().ReleasedSinceDate(indexingContext.ReleasedSince))
                {
                    members.Add(item);
                }
            }
        }

        if (membersFound != delta)
        {
            logger.LogInformation(
                "YouTube uploads-head count mismatch: playlist-id='{PlaylistId}' stored-count='{StoredCount}' count='{Count}' members-found='{MembersFound}'.",
                playlistId.PlaylistId, hints.PlaylistItemCount, total, membersFound);
            return YouTubeUploadsHeadResult.Fallback(PlaylistCountChanged, checkedCount);
        }

        return new YouTubeUploadsHeadResult(members, total, newest.Date, newest.Item?.Snippet?.ResourceId?.VideoId,
            checkedCount, null);
    }

    private static string FailureReason(YouTubePlaylistFetchFailure? failure, string otherwise) =>
        failure == YouTubePlaylistFetchFailure.QuotaExhausted ? QuotaExhausted : otherwise;
}
