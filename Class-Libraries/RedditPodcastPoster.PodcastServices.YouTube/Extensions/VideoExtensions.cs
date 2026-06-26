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
