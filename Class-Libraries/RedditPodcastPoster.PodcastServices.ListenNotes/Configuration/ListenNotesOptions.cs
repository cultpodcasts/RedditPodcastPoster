namespace RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;

public class ListenNotesOptions
{
    public required string Key { get; set; }

    public required int RequestDelaySeconds { get; set; }
    public bool UsePublishedAfter { get; set; } = false;
}