using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public class SimpleEpisodePaginator : IPaginator
{
    /// <summary>
    /// Cap subsequent page fetches for unordered (expensive) date-scoped catalogue walks.
    /// Reverse-chronological walks have no page cap and stop via ReleasedSince instead.
    /// </summary>
    private const int UnorderedPageCap = 20;

    private readonly DateTime? _releasedSince;
    private readonly bool _isInReverseOrder;
    private readonly int? _maxPages;
    private readonly ILogger<SimpleEpisodePaginator> _logger;

    public SimpleEpisodePaginator(
        DateTime? releasedSince,
        bool isInReverseOrder,
        ILogger<SimpleEpisodePaginator> logger)
        : this(releasedSince, isInReverseOrder, isInReverseOrder ? null : UnorderedPageCap, logger)
    {
    }

    /// <summary>
    /// Test-only overload allowing a custom subsequent-page cap.
    /// </summary>
    internal SimpleEpisodePaginator(
        DateTime? releasedSince,
        bool isInReverseOrder,
        int? maxPages,
        ILogger<SimpleEpisodePaginator> logger)
    {
        _releasedSince = releasedSince;
        _isInReverseOrder = isInReverseOrder;
        _maxPages = maxPages;
        _logger = logger;
    }

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
                if (!_releasedSince.HasValue || episode.GetReleaseDate() >= _releasedSince)
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
               (!_maxPages.HasValue || pagesFetched < _maxPages.Value) &&
               (!_isInReverseOrder ||
                !_releasedSince.HasValue ||
                page.Items.All(x => x == null) ||
                (_isInReverseOrder && lastItem != null && lastItem.GetReleaseDate() >= _releasedSince)))
        {
            try
            {
                page = await connector.Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel)
                    .ConfigureAwait(false);
                pagesFetched++;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error paging {pageNext}",
                    firstPage.Next);
                yield break;
            }

            foreach (var item in page.Items!)
            {
                if (item is SimpleEpisode episode)
                {
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (episode == null || !_releasedSince.HasValue || episode.GetReleaseDate() >= _releasedSince)
                    {
                        yield return item;
                    }

                    lastItem = episode;
                }
            }
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
