namespace RedditPodcastPoster.Tmdb.Configuration;

/// <summary>
/// TMDB API Read Access Token. Sent as <c>Authorization: Bearer</c>.
/// Not a v3 query <c>api_key</c>. App setting <c>tmdb__ApiKey</c>; Key Vault secret <c>Tmdb-ApiKey</c>.
/// </summary>
public class TmdbOptions
{
    public string ApiKey { get; set; } = "";
}
