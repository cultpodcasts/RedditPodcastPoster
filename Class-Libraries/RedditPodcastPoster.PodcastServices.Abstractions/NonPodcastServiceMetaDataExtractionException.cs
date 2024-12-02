using System.Net;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public class NonPodcastServiceMetaDataExtractionException : Exception
{
    public NonPodcastServiceMetaDataExtractionException(Uri uri, HttpStatusCode statusCode) : base(
        $"Uri '{uri}' failed with status-code '{statusCode}'.")
    {
    }

    public NonPodcastServiceMetaDataExtractionException(Uri uri, string message) : base(
        $"Uri '{uri}' failed. {message}")
    {
    }
}