using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Paginators;

/// <summary>
/// Reads an oldest-first Spotify catalogue from its newest end. Spotify paging responses expose
/// Total and Limit, so a date-scoped lookup can jump directly to the final page and then walk
/// backwards only while episodes remain inside the release window.
/// </summary>
public sealed class AscendingEpisodePaginator(
    DateTime releasedSince,
    ILogger<AscendingEpisodePaginator> logger,
    IPaginator forwardFallbackPaginator) : IPaginator
{
    public Task<IList<T>> PaginateAll<T>(
        IPaginatable<T> firstPage,
        IAPIConnector connector,
        CancellationToken cancel = new()) =>
        throw new NotImplementedException();

    public Task<IList<T>> PaginateAll<T, TNext>(
        IPaginatable<T, TNext> firstPage,
        Func<TNext, IPaginatable<T, TNext>> mapper,
        IAPIConnector connector,
        CancellationToken cancel = new()) =>
        throw new NotImplementedException();

    public async IAsyncEnumerable<T> Paginate<T>(
        IPaginatable<T> firstPage,
        IAPIConnector connector,
        [EnumeratorCancellation] CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        ArgumentNullException.ThrowIfNull(connector);

        if (firstPage is not Paging<T> firstPaging ||
            firstPaging.Items == null ||
            firstPaging.Total <= 0 ||
            firstPaging.Limit <= 0)
        {
            logger.LogWarning(
                "Spotify ascending pagination could not jump to catalogue end because paging metadata was unavailable; using bounded forward fallback.");

            await foreach (var item in forwardFallbackPaginator.Paginate(firstPage, connector, cancel))
            {
                yield return item;
            }

            yield break;
        }

        var total = firstPaging.Total!.Value;
        var limit = firstPaging.Limit!.Value;
        var finalOffset = Math.Max(0, ((total - 1) / limit) * limit);
        var page = firstPaging;
        var pagesFetched = 0;

        if (firstPaging.Offset != finalOffset)
        {
            var finalPageUri = BuildPageUri(firstPaging, finalOffset);
            page = await connector.Get<Paging<T>>(finalPageUri, cancel).ConfigureAwait(false);
            pagesFetched++;

            logger.LogInformation(
                "Spotify ascending pagination jumped to final page: total='{Total}' limit='{Limit}' offset='{Offset}'.",
                total,
                limit,
                finalOffset);
        }

        while (true)
        {
            var crossedCutoff = false;
            foreach (var item in page.Items ?? [])
            {
                if (item is not SimpleEpisode episode)
                {
                    continue;
                }

                if (episode.GetReleaseDate() >= releasedSince)
                {
                    yield return item;
                }
                else
                {
                    crossedCutoff = true;
                }
            }

            // In a genuinely ascending catalogue every preceding page is older. Once this page
            // contains an out-of-window episode, no earlier page can add an in-window result.
            if (crossedCutoff || page.Previous == null)
            {
                yield break;
            }

            if (pagesFetched >= SimpleEpisodePaginator.MaxPages)
            {
                logger.LogError(
                    SimpleEpisodePaginator.CircuitBreakerTrippedMessageTemplate,
                    pagesFetched,
                    SimpleEpisodePaginator.MaxPages,
                    releasedSince,
                    page.Previous);
                yield break;
            }

            page = await connector.Get<Paging<T>>(
                    new Uri(NormalizeShowEpisodesPath(page.Previous), UriKind.Absolute),
                    cancel)
                .ConfigureAwait(false);
            pagesFetched++;
        }
    }

    public IAsyncEnumerable<T> Paginate<T, TNext>(
        IPaginatable<T, TNext> firstPage,
        Func<TNext, IPaginatable<T, TNext>> mapper,
        IAPIConnector connector,
        CancellationToken cancel = new()) =>
        throw new NotImplementedException();

    private static Uri BuildPageUri<T>(Paging<T> page, int offset)
    {
        var source = page.Href ?? page.Next ??
                     throw new InvalidOperationException(
                         "Spotify paging response did not provide Href or Next for the final-page jump.");
        source = NormalizeShowEpisodesPath(source);

        var offsetPattern = new Regex(@"([?&])offset=\d+", RegexOptions.IgnoreCase);
        source = offsetPattern.IsMatch(source)
            ? offsetPattern.Replace(source, $"$1offset={offset}", 1)
            : $"{source}{(source.Contains('?') ? '&' : '?')}offset={offset}";

        var limit = page.Limit!.Value;
        var limitPattern = new Regex(@"([?&])limit=\d+", RegexOptions.IgnoreCase);
        source = limitPattern.IsMatch(source)
            ? limitPattern.Replace(source, $"$1limit={limit}", 1)
            : $"{source}&limit={limit}";

        return new Uri(source, UriKind.Absolute);
    }

    private static string NormalizeShowEpisodesPath(string url) =>
        url.Replace("/v1/show/", "/v1/shows/", StringComparison.OrdinalIgnoreCase);
}
