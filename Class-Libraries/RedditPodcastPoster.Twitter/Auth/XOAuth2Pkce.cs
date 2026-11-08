using System.Security.Cryptography;
using System.Text;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// PKCE (S256) for the X authorization-code login. The verifier stays in this process; only the challenge
/// goes on the authorize URL. See https://docs.x.com/resources/fundamentals/authentication/oauth-2-0/authorization-code.
/// </summary>
public static class XOAuth2Pkce
{
    public const string AuthorizeEndpoint = "https://twitter.com/i/oauth2/authorize";
    public const string ChallengeMethod = "S256";

    public static XOAuth2PkcePair Create()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        return new XOAuth2PkcePair(verifier, Challenge(verifier));
    }

    public static string Challenge(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64Url(hash);
    }

    public static Uri AuthorizeUrl(string clientId, Uri redirectUri, string state, string codeChallenge,
        IReadOnlyList<string> scopes)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri.AbsoluteUri,
            ["scope"] = string.Join(' ', scopes),
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = ChallengeMethod
        };
        var encoded = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return new Uri($"{AuthorizeEndpoint}?{encoded}");
    }

    public static string NewState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    /// <summary>Loopback only. The URI must be registered on the X app exactly, including the port and path.</summary>
    public static bool IsLoopbackRedirect(Uri uri) =>
        uri.IsAbsoluteUri
        && uri.Scheme == Uri.UriSchemeHttp
        && (uri.Host.Equals("127.0.0.1", StringComparison.Ordinal) || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        && uri.Port is > 0 and <= 65535
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record XOAuth2PkcePair(string Verifier, string Challenge);
