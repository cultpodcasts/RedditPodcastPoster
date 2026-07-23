namespace RedditPodcastPoster.Auth0.Configuration;

/// <summary>
/// Optional staging Auth0 trust for api-preview. Bound from <c>auth0__Staging__*</c>.
/// </summary>
public class Auth0StagingValidationOptions
{
    /// <summary>
    /// When false (default), staging issuer is ignored even if Domain/Issuer are set.
    /// </summary>
    public bool Trust { get; set; }

    /// <summary>Auth0 custom domain (e.g. auth-staging.cultpodcasts.com).</summary>
    public string? Domain { get; set; }

    /// <summary>Issuer URL including trailing slash (e.g. https://auth-staging.cultpodcasts.com/).</summary>
    public string? Issuer { get; set; }
}

public class Auth0ValidationOptions
{
    public required string Audience { get; set; }
    public required string Domain { get; set; }
    public required string Issuer { get; set; }

    /// <summary>Staging Auth0 settings (<c>auth0__Staging__Domain</c>, etc.).</summary>
    public Auth0StagingValidationOptions? Staging { get; set; }

    public bool TrustsStagingIssuer =>
        Staging is { Trust: true } &&
        !string.IsNullOrWhiteSpace(Staging.Domain) &&
        !string.IsNullOrWhiteSpace(Staging.Issuer);

    public IReadOnlyList<string> GetTrustedIssuers()
    {
        if (!TrustsStagingIssuer)
        {
            return [Issuer];
        }

        return [Issuer, Staging!.Issuer!];
    }

    public IReadOnlyList<string> GetTrustedDomains()
    {
        if (!TrustsStagingIssuer)
        {
            return [Domain];
        }

        return [Domain, Staging!.Domain!];
    }
}
