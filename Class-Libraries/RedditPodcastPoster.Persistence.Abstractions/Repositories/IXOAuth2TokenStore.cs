using RedditPodcastPoster.Models.Social;

namespace RedditPodcastPoster.Persistence.Abstractions.Repositories;

/// <summary>
/// The X OAuth 2.0 token pair singleton in LookUps. Reads return the document with its etag; every write is
/// If-Match on that etag, so the refresh lease can only be held by one refresher across hosts.
/// </summary>
public interface IXOAuth2TokenStore
{
    /// <summary>The current document and its etag, or null when no token exchange has been saved yet.</summary>
    Task<XOAuth2TokenSnapshot?> Get(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a refresh lease for <paramref name="attemptId"/> lasting until <paramref name="leaseUntilUtc"/>,
    /// If-Match on <paramref name="current"/>'s etag (or create-only when <paramref name="current"/> is null).
    /// Returns the leased snapshot, or null when another refresher changed the document first.
    /// The caller decides whether a lease already in the document is still live.
    /// </summary>
    Task<XOAuth2TokenSnapshot?> TryTakeRefreshLease(
        XOAuth2TokenSnapshot? current,
        Guid attemptId,
        DateTime leaseUntilUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a new token pair and clears the lease and any needs-reauth marker, If-Match on the leased etag. If the
    /// etag no longer matches (the lease was taken over), the document is re-read: when it still holds the refresh
    /// token this attempt spent, the pair is saved unconditionally (X has already invalidated the old refresh token,
    /// so losing the new one would strand the account); when another host has already saved a newer pair, that pair
    /// is kept and returned instead. Use the returned snapshot's access token.
    /// </summary>
    Task<XOAuth2TokenSnapshot> SaveTokens(
        XOAuth2TokenSnapshot leased,
        XOAuth2TokenGrant grant,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the stored grant as needing re-authorisation (and clears the lease), If-Match on the leased etag.
    /// Returns false when another host changed the document first.
    /// </summary>
    Task<bool> MarkNeedsReauth(
        XOAuth2TokenSnapshot leased,
        string? reason,
        DateTime sinceUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Clears the lease if this attempt still holds it. Never throws for a lost or missing document.</summary>
    Task ReleaseRefreshLease(XOAuth2TokenSnapshot leased, CancellationToken cancellationToken = default);
}

public sealed record XOAuth2TokenSnapshot(XOAuth2TokenState State, string? ETag);

/// <summary>A token pair returned by the X token endpoint.</summary>
public sealed record XOAuth2TokenGrant(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresUtc,
    string? Scope,
    DateTime ObtainedUtc,
    string Source)
{
    // Keep tokens out of logs and exception messages.
    public override string ToString() =>
        $"{nameof(XOAuth2TokenGrant)} {{ Source = {Source}, AccessTokenExpiresUtc = {AccessTokenExpiresUtc:O}, Scope = {Scope} }}";
}
