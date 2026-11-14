using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// LookUps claim per episode (id = <see cref="FilmTrailerClaim.IdFor"/>, partition = id). Create fails with 409 when a
/// claim exists; a takeover replaces with If-Match on the etag that was read. Nothing here deletes a claim; it expires
/// by TTL.
/// </summary>
public sealed class FilmTrailerClaimStore(
    Container lookUpsContainer,
    ILogger<FilmTrailerClaimStore> logger) : IFilmTrailerClaimStore
{
    public async Task<FilmTrailerClaimAttempt> TryClaim(Guid episodeId, Guid filmId, DateTime nowUtc)
    {
        var claim = NewClaim(episodeId, filmId, nowUtc);
        var partitionKey = new PartitionKey(claim.Id.ToString());

        // Two passes: a claim that expires between our create and our read is retried once as a fresh create.
        for (var pass = 0; pass < 2; pass++)
        {
            try
            {
                await lookUpsContainer.CreateItemAsync(claim, partitionKey);
                return new FilmTrailerClaimAttempt(true);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
            {
                // Claimed already; inspect it below.
            }

            try
            {
                var existing = await lookUpsContainer.ReadItemAsync<FilmTrailerClaim>(
                    claim.Id.ToString(), partitionKey);
                return existing.Resource.FilmId == filmId
                    ? new FilmTrailerClaimAttempt(true)
                    : new FilmTrailerClaimAttempt(false, existing.Resource, existing.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // Expired between create and read; try again.
            }
        }

        throw new InvalidOperationException($"Could not claim or read the trailer claim of episode '{episodeId}'.");
    }

    public async Task<bool> TryTakeOver(Guid episodeId, Guid filmId, DateTime nowUtc, string holderETag)
    {
        var claim = NewClaim(episodeId, filmId, nowUtc);
        try
        {
            await lookUpsContainer.ReplaceItemAsync(
                claim,
                claim.Id.ToString(),
                new PartitionKey(claim.Id.ToString()),
                new ItemRequestOptions { IfMatchEtag = holderETag });
            logger.LogWarning(
                "{method}: Took over the trailer claim of episode '{episodeId}' for film '{filmId}'.",
                nameof(TryTakeOver), episodeId, filmId);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static FilmTrailerClaim NewClaim(Guid episodeId, Guid filmId, DateTime nowUtc) => new()
    {
        Id = FilmTrailerClaim.IdFor(episodeId),
        EpisodeId = episodeId,
        FilmId = filmId,
        ClaimedUtc = nowUtc
    };
}
