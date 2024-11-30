using System.Xml;
using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class VideoExtensions
{
    public static TimeSpan? GetLength(this Google.Apis.YouTube.v3.Data.Video video)
    {
        if (string.IsNullOrWhiteSpace(video.ContentDetails.Duration))
        {
            return null;
        }

        return XmlConvert.ToTimeSpan(video.ContentDetails.Duration);
    }

    public static Uri? GetImageUrl(this Google.Apis.YouTube.v3.Data.Video? video)
    {
        Uri? imageUrl = null;

        if (video?.Snippet.Thumbnails != null)
        {
            var images = new List<(long Height, Uri ImageUrl)>();
            var (valid, imageDetails) = GetThumbnail(video.Snippet.Thumbnails.Maxres);
            if (valid && imageDetails != null)
            {
                images.Add(imageDetails.Value);
            }

            (valid, imageDetails) = GetThumbnail(video.Snippet.Thumbnails.High);
            if (valid && imageDetails != null)
            {
                images.Add(imageDetails.Value);
            }

            (valid, imageDetails) = GetThumbnail(video.Snippet.Thumbnails.Medium);
            if (valid && imageDetails != null)
            {
                images.Add(imageDetails.Value);
            }

            (valid, imageDetails) = GetThumbnail(video.Snippet.Thumbnails.Standard);
            if (valid && imageDetails != null)
            {
                images.Add(imageDetails.Value);
            }

            (valid, imageDetails) = GetThumbnail(video.Snippet.Thumbnails.Default__);
            if (valid && imageDetails != null)
            {
                images.Add(imageDetails.Value);
            }

            imageUrl = images.MaxBy(x => x.Height).ImageUrl;
        }

        return imageUrl;

        (bool, (long, Uri)?) GetThumbnail(Thumbnail thumbnail)
        {
            if (thumbnail is {Height: not null} &&
                !string.IsNullOrWhiteSpace(thumbnail.Url))
            {
                return (true, (thumbnail.Height.Value,
                    new Uri(thumbnail.Url)));
            }

            return (false, null);
        }
    }
}