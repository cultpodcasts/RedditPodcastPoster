namespace RedditPodcastPoster.Models.Extensions;

public static class PodcastExtensions
{
    public static string PodcastNameInSafeUrlForm(this Podcast podcast)
    {
        var escapedPodcastName = Uri.EscapeDataString(podcast.Name);
        var podcastName = escapedPodcastName.Replace("(", "%28").Replace(")", "%29");
        return podcastName;
    }

}