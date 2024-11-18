using System.Xml;
using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class VideoExtensions
{
    public static TimeSpan? GetLength(this Video video)
    {
        if (string.IsNullOrWhiteSpace(video.ContentDetails.Duration))
        {
            return null;
        }

        return XmlConvert.ToTimeSpan(video.ContentDetails.Duration);
    }

    public static Uri? GetImageUrl(this Video? video)
    {
        Uri? imageUrl = null;
        if (!string.IsNullOrWhiteSpace(video?.Snippet.Thumbnails?.Maxres?.Url))
        {
            imageUrl = new Uri(video.Snippet.Thumbnails.Maxres.Url);
        }
        else if (!string.IsNullOrWhiteSpace(video?.Snippet.Thumbnails?.High?.Url))
        {
            imageUrl = new Uri(video.Snippet.Thumbnails.High.Url);
        }
        else if (!string.IsNullOrWhiteSpace(video?.Snippet.Thumbnails?.Medium?.Url))
        {
            imageUrl = new Uri(video.Snippet.Thumbnails.Medium.Url);
        }
        else if (!string.IsNullOrWhiteSpace(video?.Snippet.Thumbnails?.Standard?.Url))
        {
            imageUrl = new Uri(video.Snippet.Thumbnails.Standard.Url);
        }
        else if (!string.IsNullOrWhiteSpace(video?.Snippet.Thumbnails?.Default__?.Url))
        {
            imageUrl = new Uri(video.Snippet.Thumbnails.Default__.Url);
        }

        return imageUrl;
    }
}