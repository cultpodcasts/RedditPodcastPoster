using System.Net;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Lookups;

/// <summary>
/// The film search push watermark lives in the <c>LookUps</c> container as a singleton partitioned by its own id,
/// like the other LookUps cursors. It is read and written by id only (one point read and one upsert per run).
/// </summary>
public sealed class FilmSearchPushWatermarkStore(Container lookUpsContainer) : IFilmSearchPushWatermarkStore
{
    private static readonly string Id = FilmSearchPushWatermark._Id.ToString();

    public async Task<FilmSearchPushWatermark?> GetAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await lookUpsContainer.ReadItemAsync<FilmSearchPushWatermark>(
                Id, new PartitionKey(Id), cancellationToken: cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(FilmSearchPushWatermark watermark, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(watermark);
        if (watermark.Id != FilmSearchPushWatermark._Id)
        {
            throw new ArgumentException("The film search push watermark is a singleton with a fixed id.", nameof(watermark));
        }

        await lookUpsContainer.UpsertItemAsync(watermark, new PartitionKey(Id), cancellationToken: cancellationToken);
    }
}
