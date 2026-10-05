namespace RedditPodcastPoster.Tmdb.Configuration;

/// <summary>
/// TMDB v3 API key from the TMDB account settings. Sent as the <c>api_key</c> query parameter.
/// </summary>
public class TmdbOptions
{
    public string ApiKey { get; set; } = "";
}
