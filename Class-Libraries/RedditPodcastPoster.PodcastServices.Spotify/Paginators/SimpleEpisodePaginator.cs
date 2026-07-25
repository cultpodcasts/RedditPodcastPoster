using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public class SimpleEpisodePaginator(
    DateTime? releasedSince,
    bool isInReverseOrder,
    ILogger<SimpleEpisodePaginator> logger) : IPaginator
{
    /// <summary>
    /// Circuit breaker for unordered (expensive) date-scoped catalogue walks: caps subsequent page
    /// fetches so an ascending high-volume catalogue cannot burn the Spotify quota. Tripping it is
    /// logged at Error via <see cref="CircuitBreakerTrippedMessagePrefix"/> because episodes may be missed.
    /// Reverse-chronological walks have no page cap and stop via ReleasedSince instead.
    /// </summary>
    public const int MaxPages = 20;

    public const string CircuitBreakerTrippedMessagePrefix = "Spotify pagination circuit-breaker tripped:";

    public const string CircuitBreakerTrippedMessageTemplate =
        "Spotify pagination circuit-breaker tripped: pages-fetched='{PagesFetched}' max-pages='{MaxPages}' released-since='{ReleasedSince}' next='{Next}' reverse-chronological='false'. Stopped to protect Spotify quota; in-window episodes may be missing.";

    public Task<IList<T>> PaginateAll<T>(IPaginatable<T> firstPage, IAPIConnector connector,
        CancellationToken cancel = new())
    {
        throw new NotImplementedException();
    }

    public Task<IList<T>> PaginateAll<T, TNext>(IPaginatable<T, TNext> firstPage,
        Func<TNext, IPaginatable<T, TNext>> mapper, IAPIConnector connector,
        CancellationToken cancel = new())
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<T> Paginate<T>(
        IPaginatable<T> firstPage,
        IAPIConnector connector,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        if (firstPage == null)
        {
            throw new ArgumentNullException(nameof(firstPage));
        }

        if (connector == null)
        {
            throw new ArgumentNullException(nameof(connector));
        }

        if (firstPage.Items == null)
        {
            throw new ArgumentException("The first page has to contain an Items list!", nameof(firstPage));
        }

        var page = firstPage;
        SimpleEpisode? lastItem = null;
        var pagesFetched = 0;
        foreach (var item in page.Items)
        {
            if (item is SimpleEpisode episode)
            {
                if (!releasedSince.HasValue || episode.GetReleaseDate() >= releasedSince)
                {
                    yield return item;
                }

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (episode != null)
                {
                    lastItem = episode;
                }
            }
        }

        // Unordered walks hard-cap subsequent fetches; reverse-chrono relies on ReleasedSince early-stop.
        while (page.Next != null &&
               (isInReverseOrder || pagesFetched < MaxPages) &&
               (!isInReverseOrder ||
                !releasedSince.HasValue ||
                page.Items.All(x => x == null) ||
                (isInReverseOrder && lastItem != null && lastItem.GetReleaseDate() >= releasedSince)))
        {
            try
            {
                page = await connector.Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);
                pagesFetched++;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error paging {pageNext}",
                    firstPage.Next);
                yield break;
            }

            foreach (var item in page.Items!)
            {
                if (item is SimpleEpisode episode)
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (episode == null || !releasedSince.HasValue || episode.GetReleaseDate() >= releasedSince)
                    {
                        yield return item;
                    }

                    lastItem = episode;
                }
            }
        }

        if (!isInReverseOrder && pagesFetched >= MaxPages && page.Next != null)
        {
            logger.LogError(
                CircuitBreakerTrippedMessageTemplate,
                pagesFetched,
                MaxPages,
                releasedSince,
                page.Next);
        }
    }

    public IAsyncEnumerable<T> Paginate<T, TNext>(IPaginatable<T, TNext> firstPage,
        Func<TNext, IPaginatable<T, TNext>> mapper, IAPIConnector connector,
        CancellationToken cancel = new())
    {
        throw new NotImplementedException();
    }

    protected virtual Task<bool> ShouldContinue<T>(List<T> results, IPaginatable<T> page)
    {
        return Task.FromResult(true);
    }
}
