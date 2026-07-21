using System.Xml;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class VideoExtensions
{
    public static bool IsCompletedPublicVideo(this Google.Apis.YouTube.v3.Data.Video video)
    {
        var liveBroadcastContent = video.Snippet?.LiveBroadcastContent;
        return liveBroadcastContent != "live" && liveBroadcastContent != "upcoming";
    }

    /// <summary>
    /// Detects YouTube members-only (channel-membership-gated) videos.
    /// The Data API exposes no explicit members-only flag: such videos stay publicly listed
    /// (<c>status.privacyStatus == "public"</c>, real snippet/contentDetails, <c>publicStatsViewable == true</c>),
    /// but YouTube omits <c>statistics.viewCount</c> for them while still returning the other statistics
    /// (<c>likeCount</c>, <c>favoriteCount</c>, <c>commentCount</c>). Public videos ALWAYS include
    /// <c>viewCount</c> — a brand-new upload with no views returns <c>"viewCount": "0"</c> (i.e.
    /// <see cref="VideoStatistics.ViewCount"/> == <c>0</c>, not <c>null</c>). We therefore treat an
    /// <b>absent</b> <c>viewCount</c> (property missing → <c>null</c>) on an otherwise-populated statistics
    /// object as the members-only signal. A zero view count is deliberately NOT treated as members-only.
    /// </summary>
    /// <remarks>
    /// Requires the <c>statistics</c> part to have been requested when fetching the video. When statistics
    /// were not requested, <see cref="Google.Apis.YouTube.v3.Data.Video.Statistics"/> is <c>null</c> and this
    /// returns <c>false</c> (never skips), so callers must request statistics for this check to take effect.
    /// </remarks>
    public static bool IsMembersOnly(this Google.Apis.YouTube.v3.Data.Video video)
    {
        return video.Statistics != null && video.Statistics.ViewCount == null;
    }

    public static TimeSpan? GetLength(this Google.Apis.YouTube.v3.Data.Video video)
    {
        if (string.IsNullOrWhiteSpace(video.ContentDetails.Duration))
        {
            return null;
        }

        return XmlConvert.ToTimeSpan(video.ContentDetails.Duration);
    }

    public static IReadOnlyList<ThumbnailCandidate> GetThumbnailCandidates(this Google.Apis.YouTube.v3.Data.Video? video)
    {
        if (video?.Snippet?.Thumbnails == null)
        {
            return [];
        }

        var images = new List<ThumbnailCandidate>();
        AddThumbnail(video.Snippet.Thumbnails.Maxres, false);
        AddThumbnail(video.Snippet.Thumbnails.High, false);
        AddThumbnail(video.Snippet.Thumbnails.Medium, false);
        AddThumbnail(video.Snippet.Thumbnails.Standard, false);
        AddThumbnail(video.Snippet.Thumbnails.Default__, true);

        return images.OrderByDescending(x => x.Height).ToList();

        void AddThumbnail(Thumbnail? thumbnail, bool isDefaultTier)
        {
            if (thumbnail is { Height: not null } && !string.IsNullOrWhiteSpace(thumbnail.Url))
            {
                images.Add(new ThumbnailCandidate(new Uri(thumbnail.Url), thumbnail.Height.Value, isDefaultTier));
            }
        }
    }
}
