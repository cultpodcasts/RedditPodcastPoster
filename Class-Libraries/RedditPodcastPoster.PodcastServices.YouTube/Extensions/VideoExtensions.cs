using System.Xml;
using Google.Apis.YouTube.v3.Data;

namespace RedditPodcastPoster.PodcastServices.YouTube.Extensions;

public static class VideoExtensions
{
    public static TimeSpan GetLength(this Video video)
    {
        return XmlConvert.ToTimeSpan(video.ContentDetails.Duration);
    }
}