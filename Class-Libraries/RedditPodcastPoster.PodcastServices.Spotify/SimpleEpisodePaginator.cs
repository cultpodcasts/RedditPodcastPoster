using System.Runtime.CompilerServices;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SimpleEpisodePaginator(DateTime? releasedSince, bool isInReverseOrder) : IPaginator
{
    public async Task<IList<T>> PaginateAll<T>(IPaginatable<T> firstPage, IAPIConnector connector,
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
        foreach (var item in page.Items)
        {
            if (item is SimpleEpisode episode)
            {
                if (!releasedSince.HasValue || episode.GetReleaseDate() > releasedSince)
                {
                    yield return item;
                }

                lastItem = episode;
            }
        }

        while (page.Next != null &&
               (!isInReverseOrder ||
                !releasedSince.HasValue ||
                (isInReverseOrder && lastItem!.GetReleaseDate() > releasedSince)))
        {
            page = await connector.Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel).ConfigureAwait(false);
            foreach (var item in page.Items!)
            {
                if (item is SimpleEpisode episode)
                {
                    if (!releasedSince.HasValue || episode.GetReleaseDate() > releasedSince)
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