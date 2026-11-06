using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence.Repositories;

/// <summary>
/// Shared Cosmos feed-page and count loops for the catalogue repositories.
/// Episode homepage counting stays in <see cref="EpisodeRepository"/>.
/// </summary>
public static class CatalogueFeed
{
    /// <summary>
    /// Reads the first COUNT value. The iterator is owned by this method and is disposed before the task completes.
    /// </summary>
    public static async Task<int> Count(FeedIterator<int> pages, Action<Exception> logFailure)
    {
        using (pages)
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
    }

    /// <summary>
    /// Pages a catalogue query. Repository methods return this sequence and do not take a cancellation token;
    /// <c>ToListAsync(ct)</c>, <c>CountAsync(..., ct)</c>, and <c>WithCancellation(ct)</c> deliver the consumer
    /// token through <see cref="EnumeratorCancellationAttribute"/>. The iterator is owned by this method and is
    /// disposed when enumeration ends, fails, or stops early. Callers must not use it after passing it in.
    /// </summary>
    public static async IAsyncEnumerable<T> Read<T>(
        FeedIterator<T> pages,
        Action<Exception> logFailure,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            while (pages.HasMoreResults)
            {
                FeedResponse<T> response;
                try
                {
                    response = await pages.ReadNextAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ex.Message, ex, cancellationToken);
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
        finally
        {
            pages.Dispose();
        }
    }
}
