using RedditPodcastPoster.Auth0.Configuration;
using RedditPodcastPoster.EdgeApi.Configuration;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// The resolved D1 search writer settings. The api Worker host comes from <c>api:Endpoint</c>, and the Auth0 token
/// request uses <c>auth0client:Domain</c> and <c>auth0client:Audience</c>; only the search-index-writer
/// <c>d1Search:ClientId</c> and <c>d1Search:ClientSecret</c> are D1-specific. <see cref="IsActive"/> also requires
/// <c>d1Search:Enabled</c> in the Functions (<paramref name="requireEnabled"/> true). The
/// <c>D1SearchReconcile</c> console does not: it is dry unless <c>--write</c>.
/// </summary>
public sealed class D1SearchConnection
{
    private readonly D1SearchOptions _d1Search;
    private readonly ApiOptions _api;
    private readonly Auth0Options _auth0;

    public D1SearchConnection(
        D1SearchOptions d1Search,
        ApiOptions api,
        Auth0Options auth0,
        bool requireEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(d1Search);
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(auth0);
        _d1Search = d1Search;
        _api = api;
        _auth0 = auth0;
        RequireEnabled = requireEnabled;
    }

    public bool RequireEnabled { get; set; }
    public bool Enabled => _d1Search.Enabled;
    public Uri? Endpoint => _api.Endpoint;
    public string Auth0Domain => _auth0.Domain ?? string.Empty;
    public string Audience => _auth0.Audience ?? string.Empty;
    public string ClientId => _d1Search.ClientId;
    public string ClientSecret => _d1Search.ClientSecret;
    public int RetryDelayMilliseconds => _d1Search.RetryDelayMilliseconds;

    /// <summary>Endpoint, Auth0 domain and audience, and the writer client id and secret are all present.</summary>
    public bool IsConfigured =>
        Endpoint is not null &&
        !string.IsNullOrWhiteSpace(Auth0Domain) &&
        !string.IsNullOrWhiteSpace(Audience) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    /// <summary>The writer may call the api Worker. The Functions also require <see cref="Enabled"/>.</summary>
    public bool IsActive => IsConfigured && (!RequireEnabled || Enabled);
}
