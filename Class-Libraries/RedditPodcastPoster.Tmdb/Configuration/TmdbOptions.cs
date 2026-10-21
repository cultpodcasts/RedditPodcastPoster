namespace RedditPodcastPoster.Tmdb.Configuration;

/// <summary>
/// TMDB credential. A read-access token (a JWT, containing <c>.</c>) is sent as
/// <c>Authorization: Bearer</c>. A v3 API key is sent as the <c>api_key</c> query parameter.
/// App setting <c>tmdb__ApiKey</c>; Key Vault secret <c>Tmdb-ApiKey</c>.
/// <para>
/// <b>Prefer the read-access token.</b> A v3 key in the query string ends up in any full request URL: App Insights
/// dependency telemetry on Functions hosts records it. <see cref="Clients.TmdbClient"/> logs only the relative
/// path (before the key is appended) and never <c>RequestUri</c>; keep it that way.
/// </para>
/// </summary>
public class TmdbOptions
{
    public string ApiKey { get; set; } = "";
}
