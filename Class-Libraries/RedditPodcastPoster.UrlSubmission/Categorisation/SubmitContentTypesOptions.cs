namespace RedditPodcastPoster.UrlSubmission.Categorisation;

/// <summary>
/// Gates submit v2 classification. Off (the default) persists Podcast + Episode.
/// </summary>
public class SubmitContentTypesOptions
{
    public const string SectionName = "submitContentTypes";

    public bool Enabled { get; set; }
}
