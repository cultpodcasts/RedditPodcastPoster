namespace RedditPodcastPoster.Tmdb.Configuration;

/// <summary>
/// TMDB credential. A read-access token (a JWT, containing <c>.</c>) is sent as
/// <c>Authorization: Bearer</c>. A v3 API key is sent as the <c>api_key</c> query parameter.
/// App setting <c>tmdb__ApiKey</c>; Key Vault secret <c>Tmdb-ApiKey</c>.
/// </summary>
public class TmdbOptions
{
    public string ApiKey { get; set; } = "";
}
