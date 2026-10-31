using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// One-off migration: trades the posting account's OAuth 1.0a access token for an OAuth 2.0 token pair and saves
/// it to LookUps. A dry run (the default) checks configuration and the saved state and sends nothing. Each real
/// exchange invalidates the previous OAuth 2.0 pair, so it takes the refresh lease first. Reports never contain
/// token values.
/// </summary>
public interface IXOAuth2TokenExchanger
{
    Task<XOAuth2TokenExchangeResult> Run(bool execute, CancellationToken cancellationToken = default);
}

public enum XOAuth2TokenExchangeOutcome
{
    DryRun = 1,
    Exchanged,
    MissingConfiguration,
    RefreshInProgress,
    ExchangeFailed
}

public sealed record XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome Outcome, IReadOnlyList<string> Report)
{
    public bool Succeeded => Outcome is XOAuth2TokenExchangeOutcome.DryRun or XOAuth2TokenExchangeOutcome.Exchanged;
}

public sealed class XOAuth2TokenExchanger(
    IXOAuth2TokenStore tokenStore,
    IXOAuth2TokenClient tokenClient,
    IOptions<TwitterOptions> options,
    TimeProvider timeProvider,
    ILogger<XOAuth2TokenExchanger> logger) : IXOAuth2TokenExchanger
{
    public static readonly string[] RequiredScopes = ["tweet.read", "tweet.write", "users.read", "offline.access"];
    private readonly TwitterOptions _options = options.Value;

    public async Task<XOAuth2TokenExchangeResult> Run(bool execute, CancellationToken cancellationToken = default)
    {
        var report = new List<string>
        {
            execute
                ? "Mode: EXECUTE. The OAuth 1.0a token will be exchanged and the new OAuth 2.0 pair saved."
                : "Mode: dry run. Nothing is sent to X and nothing is written. Pass --execute to exchange.",
            $"twitter:AuthMode is {_options.AuthMode}."
        };

        var missing = MissingSettings().ToArray();
        foreach (var setting in new[] {"AccessToken", "AccessTokenSecret", "OAuth2ClientId", "OAuth2ClientSecret"})
        {
            report.Add($"twitter:{setting}: {(missing.Contains(setting) ? "MISSING" : "set")}");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var snapshot = await tokenStore.Get(cancellationToken);
        report.Add(Describe(snapshot, now));

        if (missing.Length > 0)
        {
            report.Add("Cannot exchange until the missing settings are provided.");
            return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.MissingConfiguration, report);
        }

        if (snapshot?.State.IsRefreshLeaseHeld(now) == true)
        {
            report.Add("A token refresh is in progress on another host. Try again in a minute.");
            return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.RefreshInProgress, report);
        }

        if (!execute)
        {
            report.Add(
                $"Would POST {new Uri(_options.ApiBaseAddress, "2/oauth2/token")} (grant_type=urn:ietf:params:oauth:grant-type:token-exchange) and save the pair to LookUps.");
            if (snapshot?.State.HasTokens == true)
            {
                report.Add("Note: exchanging again replaces, and invalidates, the OAuth 2.0 pair saved above.");
            }

            return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.DryRun, report);
        }

        var leased = await tokenStore.TryTakeRefreshLease(snapshot, Guid.NewGuid(),
            now + XOAuth2AccessTokenProvider.LeaseDuration, cancellationToken);
        if (leased is null)
        {
            report.Add("Another host changed the token document while this ran. Try again in a minute.");
            return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.RefreshInProgress, report);
        }

        var result = await tokenClient.ExchangeOAuth1(_options.AccessToken!, _options.AccessTokenSecret!,
            cancellationToken);
        if (result is not {Outcome: XOAuth2TokenOutcome.Success, Grant: not null})
        {
            await tokenStore.ReleaseRefreshLease(leased, CancellationToken.None);
            report.Add($"Exchange failed: {result.Outcome} (HTTP {(int?) result.StatusCode}, error '{result.Error}'). Nothing was saved.");
            report.Add(result.Outcome switch
            {
                XOAuth2TokenOutcome.ClientCredentialsInvalid =>
                    "Check OAuth 2.0 is enabled on the same X app (confidential client) and the Client ID/Secret are right.",
                XOAuth2TokenOutcome.Rejected =>
                    "X did not accept the OAuth 1.0a token (invalid_grant): it must come from the same X app. Do not retry blindly.",
                _ => "Transient failure: try again shortly."
            });
            return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.ExchangeFailed, report);
        }

        await tokenStore.SaveTokens(leased, result.Grant, CancellationToken.None);
        logger.LogInformation("X OAuth 2.0 token exchange saved a new token pair (expires {ExpiresUtc:O}).",
            result.Grant.AccessTokenExpiresUtc);
        report.Add(
            $"Exchanged and saved. Access token expires {result.Grant.AccessTokenExpiresUtc:O} (UTC); it is refreshed automatically once twitter:AuthMode is OAuth2.");
        report.Add($"Granted scopes: {result.Grant.Scope}");
        var granted = (result.Grant.Scope ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lacking = RequiredScopes.Except(granted).ToArray();
        if (lacking.Length > 0)
        {
            report.Add($"WARNING: missing scopes {string.Join(", ", lacking)}. The X app needs Read and write permission.");
        }

        return new XOAuth2TokenExchangeResult(XOAuth2TokenExchangeOutcome.Exchanged, report);
    }

    private IEnumerable<string> MissingSettings()
    {
        if (string.IsNullOrWhiteSpace(_options.AccessToken)) yield return "AccessToken";
        if (string.IsNullOrWhiteSpace(_options.AccessTokenSecret)) yield return "AccessTokenSecret";
        if (string.IsNullOrWhiteSpace(_options.OAuth2ClientId)) yield return "OAuth2ClientId";
        if (string.IsNullOrWhiteSpace(_options.OAuth2ClientSecret)) yield return "OAuth2ClientSecret";
    }

    private static string Describe(XOAuth2TokenSnapshot? snapshot, DateTime now)
    {
        if (snapshot is null || !snapshot.State.HasTokens)
        {
            return "Saved OAuth 2.0 pair: none.";
        }

        var state = snapshot.State;
        var expiry = state.IsAccessTokenValid(now, TimeSpan.Zero) ? "valid" : "expired";
        return
            $"Saved OAuth 2.0 pair: from {state.Source} at {state.UpdatedUtc:O}; access token {expiry} (expires {state.AccessTokenExpiresUtc:O}); scopes '{state.Scope}'.";
    }
}
