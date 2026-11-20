namespace RedditPodcastPoster.Twitter.Configuration;

public class TwitterOptions
{
    /// <summary>Configuration section bound by <c>AddXOAuth2TokenServices</c> / posting hosts.</summary>
    public const string SectionName = "twitter";

    /// <summary>
    /// Which credentials sign X API requests. <see cref="TwitterAuthMode.OAuth1"/> (the default) keeps the
    /// OAuth 1.0a keys below; <see cref="TwitterAuthMode.OAuth2"/> sends the OAuth 2.0 user-context bearer token
    /// saved in LookUps by XOAuth2TokenExchange or XOAuth2Authorize. X stops accepting OAuth 1.0a on 1 Jan 2027.
    /// After that date only XOAuth2Authorize (PKCE) can replace a refused refresh token.
    /// </summary>
    public TwitterAuthMode AuthMode { get; set; } = TwitterAuthMode.OAuth1;

    /// <summary>OAuth 1.0a API key. Also used once by the token exchange. Remove after the OAuth 2.0 cut-over.</summary>
    public string? ConsumerKey { get; set; }
    /// <summary>OAuth 1.0a API key secret. Remove after the OAuth 2.0 cut-over.</summary>
    public string? ConsumerSecret { get; set; }
    /// <summary>OAuth 1.0a access token; the token exchange trades it for an OAuth 2.0 pair.</summary>
    public string? AccessToken { get; set; }
    /// <summary>OAuth 1.0a access token secret; sent with the token exchange.</summary>
    public string? AccessTokenSecret { get; set; }

    /// <summary>OAuth 2.0 Client ID (confidential client) from the X Developer Console.</summary>
    public string? OAuth2ClientId { get; set; }
    /// <summary>OAuth 2.0 Client Secret (confidential client) from the X Developer Console.</summary>
    public string? OAuth2ClientSecret { get; set; }

    /// <summary>X API host. Defaults to <c>https://api.x.com</c>.</summary>
    public Uri ApiBaseAddress { get; set; } = new("https://api.x.com/");

    /// <summary>Per-request timeout for X API and token calls, in seconds.</summary>
    public int RequestTimeoutSeconds { get; set; } = 15;

    public string? HashTag { get; set; }
    public bool WithEpisodeUrl { get; set; }
    /// <summary>
    /// When true and shortener KV has a share image, posts use only the short URL
    /// (no platform link). Default false — ship dark until SEO/social rollout is ready.
    /// </summary>
    public bool ShortUrlOnlyWhenShareImage { get; set; }

    /// <summary>
    /// When true, tweets append the podcast and guest X @handles (programmatic
    /// mentions). Default false — X restricts unsolicited programmatic @mentions
    /// on self-serve tiers (#1202 / Feb 2026 changelog); failed posts otherwise
    /// retry hourly without saving.
    /// </summary>
    public bool IncludeAtMentions { get; set; }

    public long TwitterId { get; set; }

    /// <summary>
    /// Inclusive upper bound for <see cref="MaxPosts"/>. A typo such as 20 or 200 must not pass
    /// validation, because X bills per post.
    /// </summary>
    public const int MaxPostsLimit = 10;

    /// <summary>
    /// Inclusive upper bound for <see cref="MaxFailures"/>.
    /// </summary>
    public const int MaxFailuresLimit = 20;

    /// <summary>
    /// Maximum tweets sent in one run. Podcast episodes are posted first; TV, news and film items
    /// fill any remaining slots. Defaults to 1, which is the historical one-tweet-per-run cadence.
    /// Must be between 1 and <see cref="MaxPostsLimit"/> inclusive. Production Functions set this
    /// with <c>twitter__MaxPosts</c>.
    /// </summary>
    public int MaxPosts { get; set; } = 1;

    /// <summary>
    /// Maximum unsuccessful posts in one run before the run stops. Run-wide X refusals
    /// (rate limit, depleted credits, rejected credentials) stop the run immediately and
    /// do not use this budget. Untweetable and DuplicateForbidden results are skips, not
    /// failures: those documents are already resolved and do not spend this budget. Defaults to 5,
    /// matching Bluesky. Must be between 1 and <see cref="MaxFailuresLimit"/> inclusive.
    /// </summary>
    public int MaxFailures { get; set; } = 5;
}

public enum TwitterAuthMode
{
    OAuth1 = 0,
    OAuth2 = 1
}
