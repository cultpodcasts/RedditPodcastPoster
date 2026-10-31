using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// X OAuth 2.0 token singleton in LookUps (fixed id, partitioned by its own id). Lease and token writes are
/// If-Match on the etag that was read, so two hosts cannot both refresh with the same single-use refresh token.
/// Never logs token values.
/// </summary>
public sealed class XOAuth2TokenStore(
    Container lookUpsContainer,
    ILogger<XOAuth2TokenStore> logger) : IXOAuth2TokenStore
{
    private static readonly string Id = XOAuth2TokenState._Id.ToString();
    private static readonly PartitionKey Partition = new(Id);

    public async Task<XOAuth2TokenSnapshot?> Get(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await lookUpsContainer.ReadItemAsync<XOAuth2TokenState>(
                Id, Partition, cancellationToken: cancellationToken);
            return new XOAuth2TokenSnapshot(response.Resource, response.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<XOAuth2TokenSnapshot?> TryTakeRefreshLease(
        XOAuth2TokenSnapshot? current,
        Guid attemptId,
        DateTime leaseUntilUtc,
        CancellationToken cancellationToken = default)
    {
        var leased = Copy(current?.State ?? new XOAuth2TokenState());
        leased.RefreshLeaseAttemptId = attemptId;
        leased.RefreshLeaseUntilUtc = leaseUntilUtc;
        try
        {
            ItemResponse<XOAuth2TokenState> written = current is null
                ? await lookUpsContainer.CreateItemAsync(leased, Partition, cancellationToken: cancellationToken)
                : await lookUpsContainer.ReplaceItemAsync(leased, Id, Partition,
                    new ItemRequestOptions { IfMatchEtag = current.ETag }, cancellationToken);
            return new XOAuth2TokenSnapshot(leased, written.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed
                                             or HttpStatusCode.Conflict or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "{method}: Another X token refresher changed the token document first ({status}).",
                nameof(TryTakeRefreshLease), ex.StatusCode);
            return null;
        }
    }

    public async Task<XOAuth2TokenSnapshot> SaveTokens(
        XOAuth2TokenSnapshot leased,
        XOAuth2TokenGrant grant,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leased);
        ArgumentNullException.ThrowIfNull(grant);
        var saved = Copy(leased.State);
        saved.AccessToken = grant.AccessToken;
        saved.RefreshToken = grant.RefreshToken;
        saved.AccessTokenExpiresUtc = grant.AccessTokenExpiresUtc;
        saved.Scope = grant.Scope;
        saved.UpdatedUtc = grant.ObtainedUtc;
        saved.Source = grant.Source;
        saved.RefreshLeaseAttemptId = null;
        saved.RefreshLeaseUntilUtc = null;
        try
        {
            var written = await lookUpsContainer.ReplaceItemAsync(saved, Id, Partition,
                new ItemRequestOptions { IfMatchEtag = leased.ETag }, cancellationToken);
            return new XOAuth2TokenSnapshot(saved, written.ETag);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            // X has already rotated the refresh token; the new pair is the only one that works, so it must win.
            logger.LogWarning(
                "{method}: The X token lease was lost before the new token pair was saved ({status}); saving it unconditionally.",
                nameof(SaveTokens), ex.StatusCode);
            var written = await lookUpsContainer.UpsertItemAsync(saved, Partition, cancellationToken: cancellationToken);
            return new XOAuth2TokenSnapshot(saved, written.ETag);
        }
    }

    public async Task ReleaseRefreshLease(XOAuth2TokenSnapshot leased, CancellationToken cancellationToken = default)
    {
        var released = Copy(leased.State);
        released.RefreshLeaseAttemptId = null;
        released.RefreshLeaseUntilUtc = null;
        try
        {
            await lookUpsContainer.ReplaceItemAsync(released, Id, Partition,
                new ItemRequestOptions { IfMatchEtag = leased.ETag }, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            logger.LogInformation(
                "{method}: The X token lease was already released or taken over ({status}).",
                nameof(ReleaseRefreshLease), ex.StatusCode);
        }
        catch (Exception ex)
        {
            // A leftover lease lapses on its own and the next refresher takes it over.
            logger.LogWarning(ex, "{method}: Failed to release the X token lease.", nameof(ReleaseRefreshLease));
        }
    }

    private static XOAuth2TokenState Copy(XOAuth2TokenState state) => new()
    {
        AccessToken = state.AccessToken,
        AccessTokenExpiresUtc = state.AccessTokenExpiresUtc,
        RefreshToken = state.RefreshToken,
        Scope = state.Scope,
        UpdatedUtc = state.UpdatedUtc,
        Source = state.Source,
        RefreshLeaseAttemptId = state.RefreshLeaseAttemptId,
        RefreshLeaseUntilUtc = state.RefreshLeaseUntilUtc
    };
}
