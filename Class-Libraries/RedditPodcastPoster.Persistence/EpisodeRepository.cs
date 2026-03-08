using System.Linq.Expressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Persistence;

public class EpisodeRepository(
    Container container,
    ILogger<EpisodeRepository> logger)
    : IEpisodeRepository
{
    private static readonly PartitionKey EpisodePartitionKey = new(ModelType.Episode.ToString());

    public async Task<Episode?> GetEpisode(Guid podcastId, Guid episodeId)
    {
        return await GetBy(x => x.PodcastId == podcastId && x.Id == episodeId);
    }

    public IAsyncEnumerable<Episode> GetByPodcastId(Guid podcastId)
    {
        return GetAllBy(x => x.PodcastId == podcastId);
    }

    public async Task Save(Episode episode)
    {
        await container.UpsertItemAsync(episode, EpisodePartitionKey);
    }

    public async Task Save(IEnumerable<Episode> episodes)
    {
        foreach (var episode in episodes)
        {
            await Save(episode);
        }
    }

    public async Task Delete(Guid podcastId, Guid episodeId)
    {
        var episode = await GetEpisode(podcastId, episodeId);
        if (episode == null)
        {
            return;
        }

        await container.DeleteItemAsync<Episode>(episodeId.ToString(), EpisodePartitionKey);
    }

    public async Task<Episode?> GetBy(Expression<Func<Episode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = EpisodePartitionKey
            })
            .Where(selector);

        var items = query.ToFeedIterator();
        if (items.HasMoreResults)
        {
            foreach (var item in await items.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async IAsyncEnumerable<Episode> GetAllBy(Expression<Func<Episode, bool>> selector)
    {
        var query = container
            .GetItemLinqQueryable<Episode>(requestOptions: new QueryRequestOptions
            {
                PartitionKey = EpisodePartitionKey
            })
            .Where(selector);

        var items = query.ToFeedIterator();
        while (items.HasMoreResults)
        {
            FeedResponse<Episode> response;
            try
            {
                response = await items.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{method}: error retrieving episodes.", nameof(GetAllBy));
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
