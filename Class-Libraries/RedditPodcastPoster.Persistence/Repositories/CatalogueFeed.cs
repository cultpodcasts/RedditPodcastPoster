using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Shared Cosmos feed-page and count loops for the catalogue repositories.
/// Episode homepage counting stays in <see cref="EpisodeRepository"/>.
/// </summary>
public static class CatalogueFeed
{
    public static async Task<int> Count(FeedIterator<int> pages, Action<Exception> logFailure)
    {
        while (pages.HasMoreResults)
        {
            try
            {
                foreach (var count in await pages.ReadNextAsync())
                {
                    return count;
                }
            }
            catch (Exception ex)
            {
                logFailure(ex);
                throw;
            }
        }

        return 0;
    }

    public static async IAsyncEnumerable<T> Read<T>(FeedIterator<T> pages, Action<Exception> logFailure)
    {
        while (pages.HasMoreResults)
        {
            FeedResponse<T> response;
            try
            {
                response = await pages.ReadNextAsync();
            }
            catch (Exception ex)
            {
                logFailure(ex);
                throw;
            }

            foreach (var item in response)
            {
                yield return item;
            }
        }
    }
}
