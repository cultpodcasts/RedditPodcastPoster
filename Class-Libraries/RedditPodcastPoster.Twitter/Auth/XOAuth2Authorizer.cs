using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// Obtains the first OAuth 2.0 user token, or a replacement after <c>invalid_grant</c>, by an authorization-code
/// login with PKCE. This is the only recovery path once X stops accepting OAuth 1.0a on 1 Jan 2027.
/// Dry run by default. Reports never contain the verifier, the authorization code, or token values.
/// </summary>
public interface IXOAuth2Authorizer
{
    Task<XOAuth2AuthorizeResult> Begin(bool execute, Uri redirectUri, CancellationToken cancellationToken = default);

    Task<XOAuth2AuthorizeResult> Complete(XOAuth2AuthorizationSession session, string callbackOrCode,
        CancellationToken cancellationToken = default);

    Task Abandon(XOAuth2AuthorizationSession session);
}

public enum XOAuth2AuthorizeOutcome
{
    DryRun = 1,
    AwaitingConsent,
    Authorised,
    MissingConfiguration,
    RefreshInProgress,
    ConsentFailed,
    ExchangeFailed,
    SaveFailed
}

public sealed record XOAuth2AuthorizeResult(
    XOAuth2AuthorizeOutcome Outcome,
    IReadOnlyList<string> Report,
    XOAuth2AuthorizationSession? Session = null)
{
    public bool Succeeded => Outcome is XOAuth2AuthorizeOutcome.DryRun
        or XOAuth2AuthorizeOutcome.AwaitingConsent
        or XOAuth2AuthorizeOutcome.Authorised;
}

/// <summary>Held in memory for the consent window. The verifier is not on the report and is not logged.</summary>
public sealed class XOAuth2AuthorizationSession
{
    internal XOAuth2AuthorizationSession(string codeVerifier, XOAuth2TokenSnapshot leased, string state, Uri redirectUri,
        Uri authorizeUrl)
    {
        CodeVerifier = codeVerifier;
        Leased = leased;
        State = state;
        RedirectUri = redirectUri;
        AuthorizeUrl = authorizeUrl;
    }

    internal string CodeVerifier { get; }
    internal XOAuth2TokenSnapshot Leased { get; }
    public string State { get; }
    public Uri RedirectUri { get; }
    public Uri AuthorizeUrl { get; }
}

public sealed class XOAuth2Authorizer(
    IXOAuth2TokenStore tokenStore,
    IXOAuth2TokenClient tokenClient,
    IOptions<TwitterOptions> options,
    TimeProvider timeProvider,
    ILogger<XOAuth2Authorizer> logger) : IXOAuth2Authorizer
{
    public static readonly TimeSpan ConsentLease = TimeSpan.FromMinutes(5);

    private readonly TwitterOptions _options = options.Value;

    public async Task<XOAuth2AuthorizeResult> Begin(bool execute, Uri redirectUri,
        CancellationToken cancellationToken = default)
    {
        var report = new List<string>
        {
            execute
                ? "Mode: EXECUTE. A browser login will mint a new OAuth 2.0 pair and save it, replacing any saved pair."
                : "Mode: dry run. Nothing is sent to X and nothing is written. Pass --execute to authorize.",
            $"twitter:AuthMode is {_options.AuthMode}."
        };

        var missingClient = string.IsNullOrWhiteSpace(_options.OAuth2ClientId) ||
                            string.IsNullOrWhiteSpace(_options.OAuth2ClientSecret);
        report.Add($"twitter:OAuth2ClientId: {(string.IsNullOrWhiteSpace(_options.OAuth2ClientId) ? "MISSING" : "set")}");
        report.Add($"twitter:OAuth2ClientSecret: {(string.IsNullOrWhiteSpace(_options.OAuth2ClientSecret) ? "MISSING" : "set")}");
        report.Add($"Redirect URI: {redirectUri.AbsoluteUri}");
        if (!XOAuth2Pkce.IsLoopbackRedirect(redirectUri))
        {
            report.Add("The redirect URI must be http://127.0.0.1 or http://localhost, with a port and no query.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await tokenStore.Get(cancellationToken);
        report.Add(Describe(snapshot, now));

        if (missingClient || !XOAuth2Pkce.IsLoopbackRedirect(redirectUri))
        {
            report.Add("Cannot authorize until the client credentials and a loopback redirect URI are set.");
            report.Add("Register that exact redirect URI on the X app (User authentication settings) before --execute.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.MissingConfiguration, report);
        }

        if (snapshot?.State.IsRefreshLeaseHeld(now) == true)
        {
            report.Add("A token refresh is in progress on another host. Try again in a minute.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.RefreshInProgress, report);
        }

        if (!execute)
        {
            report.Add(
                $"Would open {XOAuth2Pkce.AuthorizeEndpoint} (PKCE S256, scopes {string.Join(' ', XOAuth2TokenExchanger.RequiredScopes)}) and POST the authorization code to {new Uri(_options.ApiBaseAddress, "2/oauth2/token")}.");
            report.Add("Register the redirect URI on the X app before --execute. After 1 Jan 2027 this is the only way to replace a refused refresh token.");
            if (snapshot?.State.HasTokens == true)
            {
                report.Add("Note: authorizing again replaces, and invalidates, the OAuth 2.0 pair saved above.");
            }

            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.DryRun, report);
        }

        var leased = await tokenStore.TryTakeRefreshLease(snapshot, Guid.NewGuid(), now + ConsentLease,
            cancellationToken);
        if (leased is null)
        {
            report.Add("Another host changed the token document while this ran. Try again in a minute.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.RefreshInProgress, report);
        }

        var pkce = XOAuth2Pkce.Create();
        var state = XOAuth2Pkce.NewState();
        var url = XOAuth2Pkce.AuthorizeUrl(_options.OAuth2ClientId!, redirectUri, state, pkce.Challenge,
            XOAuth2TokenExchanger.RequiredScopes);
        report.Add($"Waiting up to {ConsentLease.TotalMinutes:0} minutes for consent. The refresh lease is held until then.");
        report.Add("Sign in as the X posting account. The URL is printed separately and is not logged.");
        logger.LogInformation("X OAuth 2.0 authorization started. Redirect host {Host}.", redirectUri.Host);
        return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.AwaitingConsent, report,
            new XOAuth2AuthorizationSession(pkce.Verifier, leased, state, redirectUri, url));
    }

    public async Task<XOAuth2AuthorizeResult> Complete(XOAuth2AuthorizationSession session, string callbackOrCode,
        CancellationToken cancellationToken = default)
    {
        var report = new List<string>();
        if (!XOAuth2AuthorizationCallback.TryRead(callbackOrCode, out var code, out var state, out var error))
        {
            await tokenStore.ReleaseRefreshLease(session.Leased, CancellationToken.None);
            report.Add(string.IsNullOrWhiteSpace(callbackOrCode)
                ? "No authorization code was received. Nothing was saved."
                : "The callback must be the full redirect URL, including this login's state. Nothing was saved.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.ConsentFailed, report);
        }

        if (!string.IsNullOrEmpty(error))
        {
            await tokenStore.ReleaseRefreshLease(session.Leased, CancellationToken.None);
            report.Add($"X did not grant consent ({error}). Nothing was saved.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.ConsentFailed, report);
        }

        if (!string.Equals(state, session.State, StringComparison.Ordinal))
        {
            await tokenStore.ReleaseRefreshLease(session.Leased, CancellationToken.None);
            report.Add("The returned state did not match this login. Nothing was saved.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.ConsentFailed, report);
        }

        var result = await tokenClient.ExchangeAuthorizationCode(code!, session.CodeVerifier,
            session.RedirectUri.AbsoluteUri, cancellationToken);
        if (result is not {Outcome: XOAuth2TokenOutcome.Success, Grant: not null})
        {
            await tokenStore.ReleaseRefreshLease(session.Leased, CancellationToken.None);
            report.Add($"Authorization failed: {result.Outcome} (HTTP {(int?) result.StatusCode}, error '{result.Error}'). Nothing was saved.");
            report.Add(result.Outcome switch
            {
                XOAuth2TokenOutcome.ClientCredentialsInvalid =>
                    "Check the X app is a confidential client with OAuth 2.0 enabled, and the Client ID and Secret match it.",
                XOAuth2TokenOutcome.Rejected =>
                    "X rejected the code. It is single-use and must match the redirect URI registered on the app. Start again; do not retry the same code.",
                _ => "Transient failure: start again shortly."
            });
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.ExchangeFailed, report);
        }

        var saved = await XOAuth2TokenPersistence.SaveRotatedPair(tokenStore, session.Leased, result.Grant, logger);
        if (saved is null)
        {
            report.Add(
                $"Authorized but NOT saved after {XOAuth2TokenPersistence.SaveAttempts} attempts. The code is spent. Check Cosmos access and run --execute again.");
            return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.SaveFailed, report);
        }

        logger.LogInformation("X OAuth 2.0 authorization saved a new token pair (expires {ExpiresUtc:O}).",
            result.Grant.AccessTokenExpiresUtc);
        report.Add(
            $"Authorized and saved. Access token expires {result.Grant.AccessTokenExpiresUtc:O} (UTC). Posting uses it once twitter:AuthMode is OAuth2.");
        report.Add($"Granted scopes: {result.Grant.Scope}");
        var granted = (result.Grant.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lacking = XOAuth2TokenExchanger.RequiredScopes.Except(granted).ToArray();
        if (lacking.Length > 0)
        {
            report.Add($"WARNING: missing scopes {string.Join(", ", lacking)}. The X app needs Read and write, plus offline.access.");
        }

        return new XOAuth2AuthorizeResult(XOAuth2AuthorizeOutcome.Authorised, report);
    }

    public Task Abandon(XOAuth2AuthorizationSession session) =>
        tokenStore.ReleaseRefreshLease(session.Leased, CancellationToken.None);

    private static string Describe(XOAuth2TokenSnapshot? snapshot, DateTime now)
    {
        if (snapshot is null || !snapshot.State.HasTokens)
        {
            return "Saved OAuth 2.0 pair: none.";
        }

        var state = snapshot.State;
        var expiry = state.IsAccessTokenValid(now, TimeSpan.Zero) ? "valid" : "expired";
        var reauth = state.NeedsReauth
            ? $" NEEDS RE-AUTHORISATION since {state.NeedsReauthSinceUtc:O} ({state.NeedsReauthReason})."
            : string.Empty;
        return
            $"Saved OAuth 2.0 pair: from {state.Source} at {state.UpdatedUtc:O}; access token {expiry} (expires {state.AccessTokenExpiresUtc:O}); scopes '{state.Scope}'.{reauth}";
    }
}

public static class XOAuth2AuthorizationCallback
{
    public static bool TryRead(string? input, out string? code, out string? state, out string? error)
    {
        code = null;
        state = null;
        error = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Query))
        {
            var query = ParseQuery(uri.Query);
            query.TryGetValue("code", out code);
            query.TryGetValue("state", out state);
            query.TryGetValue("error", out error);
        }
        else if (text.Contains("code=", StringComparison.Ordinal) || text.Contains("error=", StringComparison.Ordinal))
        {
            var query = ParseQuery(text.StartsWith('?') ? text : "?" + text);
            query.TryGetValue("code", out code);
            query.TryGetValue("state", out state);
            query.TryGetValue("error", out error);
        }

        return !string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(error);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = part.Split('=', 2);
            var key = Uri.UnescapeDataString(split[0]);
            var value = split.Length == 2 ? Uri.UnescapeDataString(split[1]) : string.Empty;
            values[key] = value;
        }

        return values;
    }
}
