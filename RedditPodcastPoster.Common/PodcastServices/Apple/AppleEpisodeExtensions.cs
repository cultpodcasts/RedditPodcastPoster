namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public static class AppleEpisodeExtensions
{
    public static AppleEpisode ToAppleEpisode(this PodcastEpisode podcastEpisode)
    {
        return new AppleEpisode(
            podcastEpisode.Id, 
            podcastEpisode.Title, 
            podcastEpisode.Release,
            podcastEpisode.Duration, 
            podcastEpisode.Url,
            podcastEpisode.Description,
            podcastEpisode.Explicit);
    }

    public static AppleEpisode ToAppleEpisode(this Record record)
    {
        return new AppleEpisode(
            long.Parse(record.Id), 
            record.Attributes.Name, 
            record.Attributes.Released,
            record.Attributes.Duration, 
            new Uri(record.Attributes.Url, UriKind.Absolute),
            record.Attributes.Description.Standard,
            record.Attributes.Explicit);
    }
}