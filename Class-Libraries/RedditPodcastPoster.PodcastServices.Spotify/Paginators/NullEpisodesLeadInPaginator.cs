using System.Runtime.CompilerServices;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

public class NullEpisodesLeadInPaginator(
    int maxConsecutiveNullEpisodes,
    int limit
) : IPaginator
{
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

    public IAsyncEnumerable<T> Paginate<T, TNext>(IPaginatable<T, TNext> firstPage,
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

        var yielded = 0;
        var page = firstPage;
        SimpleEpisode? lastItem = null;
        var currentNullEpisodes = 0;
        foreach (var item in page.Items)
        {
            if (item is SimpleEpisode episode)
            {
                yield return item;
                yielded++;
            }
            else
            {
                currentNullEpisodes++;
            }
        }

        while (currentNullEpisodes < maxConsecutiveNullEpisodes && yielded < limit && page.Next != null)
        {
            page = await connector.Get<Paging<T>>(new Uri(page.Next, UriKind.Absolute), cancel).ConfigureAwait(false);
            foreach (var item in page.Items!)
            {
                if (item is SimpleEpisode episode)
                {
                    yield return item;
                    yielded++;
                }
                else
                {
                    currentNullEpisodes++;
                }
            }
        }
    }

    protected virtual Task<bool> ShouldContinue<T>(List<T> results, IPaginatable<T> page)
    {
        return Task.FromResult(true);
    }
}