using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// LookUps claim per episode (id = <see cref="TvShowTrailerClaim.IdFor"/>, partition = id). Create fails with 409 when a
/// claim exists; a takeover replaces with If-Match on the etag that was read. Nothing here deletes a claim; it expires
/// by TTL.
/// </summary>
public sealed class TvShowTrailerClaimStore(
    Container lookUpsContainer,
    ILogger<TvShowTrailerClaimStore> logger) : ITvShowTrailerClaimStore
{
    public async Task<TvShowTrailerClaimAttempt> TryClaim(Guid episodeId, Guid tvShowId, DateTime nowUtc)
    {
        var claim = NewClaim(episodeId, tvShowId, nowUtc);
        var partitionKey = new PartitionKey(claim.Id.ToString());

        // Two passes: a claim that expires between our create and our read is retried once as a fresh create.
        for (var pass = 0; pass < 2; pass++)
        {
            try
            {
                await lookUpsContainer.CreateItemAsync(claim, partitionKey);
                return new TvShowTrailerClaimAttempt(true);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Claimed already; inspect it below.
            }

            try
            {
                var existing = await lookUpsContainer.ReadItemAsync<TvShowTrailerClaim>(
                    claim.Id.ToString(), partitionKey);
                return existing.Resource.TvShowId == tvShowId
                    ? new TvShowTrailerClaimAttempt(true)
                    : new TvShowTrailerClaimAttempt(false, existing.Resource, existing.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Expired between create and read; try again.
            }
        }

        throw new InvalidOperationException($"Could not claim or read the trailer claim of episode '{episodeId}'.");
    }

    public async Task<bool> TryTakeOver(Guid episodeId, Guid tvShowId, DateTime nowUtc, string holderETag)
    {
        var claim = NewClaim(episodeId, tvShowId, nowUtc);
        try
        {
            await lookUpsContainer.ReplaceItemAsync(
                claim,
                claim.Id.ToString(),
                new PartitionKey(claim.Id.ToString()),
                new ItemRequestOptions { IfMatchEtag = holderETag });
            logger.LogWarning(
                "{method}: Took over the trailer claim of episode '{episodeId}' for TV show '{tvShowId}'.",
                nameof(TryTakeOver), episodeId, tvShowId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static TvShowTrailerClaim NewClaim(Guid episodeId, Guid tvShowId, DateTime nowUtc) => new()
    {
        Id = TvShowTrailerClaim.IdFor(episodeId),
        EpisodeId = episodeId,
        TvShowId = tvShowId,
        ClaimedUtc = nowUtc
    };
}
