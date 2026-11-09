using System.Net;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// The X API rate-limit / usage-cap skip window lives in LookUps as a singleton partitioned by its own id,
/// like the other LookUps operational cursors. One point read and one upsert per change.
/// </summary>
public sealed class XApiRateLimitStore(Container lookUpsContainer) : IXApiRateLimitStore
{
    private static readonly string Id = XApiRateLimitState._Id.ToString();

    public async Task<XApiRateLimitState?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await lookUpsContainer.ReadItemAsync<XApiRateLimitState>(
                Id, new PartitionKey(Id), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(XApiRateLimitState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Id != XApiRateLimitState._Id)
        {
            throw new ArgumentException("The X API rate-limit state is a singleton with a fixed id.", nameof(state));
        }

        await lookUpsContainer.UpsertItemAsync(state, new PartitionKey(Id), cancellationToken: cancellationToken);
    }
}
