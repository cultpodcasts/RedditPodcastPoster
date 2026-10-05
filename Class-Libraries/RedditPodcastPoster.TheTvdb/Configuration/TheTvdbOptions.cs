namespace RedditPodcastPoster.TheTvdb.Configuration;

/// <summary>
/// TheTVDB API v4 credentials. <see cref="ApiKey"/> is the project key from the
/// TheTVDB dashboard. <see cref="Pin"/> is required only for a subscriber-supported key.
/// </summary>
public class TheTvdbOptions
{
    public string ApiKey { get; set; } = "";

    public string? Pin { get; set; }
}
