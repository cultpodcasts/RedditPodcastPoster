using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.YouTube.Paging;

/// <summary>Why a YouTube page walk stopped before the API stopped returning a next-page token.</summary>
public enum YouTubePageWalkStopReason
{
    /// <summary>Keep walking (or the walk ended normally because no next-page token was returned).</summary>
    None,

    /// <summary>The response's next-page token equals the token just requested: following it would loop forever.</summary>
    RepeatedPageToken,

    /// <summary>A page returned zero items but still carried a next-page token.</summary>
    EmptyPage,

    /// <summary>Every item on the page had already been seen in this walk.</summary>
    NoNewItems,

    /// <summary>The walk fetched more pages than <c>ceil(totalResults / pageSize) + 2</c>.</summary>
    PageCapExceeded
}

/// <summary>
/// Loop guard for YouTube <c>pageToken</c> walks. YouTube can keep returning a non-null
/// <c>nextPageToken</c> after the last item: on some playlists the token points at an empty page
/// whose own <c>nextPageToken</c> is the same token again. A walk that only stops on a null token
/// then re-fetches that empty page until the function times out (1 quota unit per call).
/// Call <see cref="Observe"/> after each page; stop when it returns anything other than
/// <see cref="YouTubePageWalkStopReason.None"/> and keep the items gathered so far.
/// </summary>
public sealed class YouTubePageWalkGuard
{
    /// <summary>Extra pages allowed over <c>ceil(totalResults / pageSize)</c> before the cap trips.</summary>
    public const int PageCapSlack = 2;

    public const string StoppedMessagePrefix = "YouTube page walk stopped:";

    public const string StoppedMessageTemplate =
        StoppedMessagePrefix +
        " reason='{Reason}' walk='{Walk}' id='{WalkId}' source='{Source}' source-identifier='{SourceIdentifier}' " +
        "pages-fetched='{PagesFetched}' total-results='{TotalResults}' page-size='{PageSize}' " +
        "items-collected='{ItemsCollected}'. Keeping the items gathered so far.";

    private readonly HashSet<string> _seenIds = new(StringComparer.Ordinal);

    public YouTubePageWalkGuard(int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        PageSize = pageSize;
    }

    public int PageSize { get; }

    public int PagesFetched { get; private set; }

    /// <summary>The latest <c>pageInfo.totalResults</c> reported by the API, when present.</summary>
    public int? TotalResults { get; private set; }

    /// <summary><c>ceil(totalResults / pageSize) + 2</c>, or null when the API has not reported a total.</summary>
    public int? PageCap => TotalResults is { } total
        ? (int) Math.Ceiling(Math.Max(total, 0) / (double) PageSize) + PageCapSlack
        : null;

    /// <summary>
    /// Records one fetched page and says whether the walk must stop. A null or empty
    /// <paramref name="nextPageToken"/> is the normal end and always returns <see cref="YouTubePageWalkStopReason.None"/>.
    /// </summary>
    /// <param name="requestedPageToken">The token sent for this page ("" or null for the first page).</param>
    /// <param name="itemIds">The id of every item on the page (video id), null when an item has none.</param>
    /// <param name="nextPageToken">The response's next-page token.</param>
    /// <param name="totalResults">The response's <c>pageInfo.totalResults</c>, when present.</param>
    public YouTubePageWalkStopReason Observe(
        string? requestedPageToken,
        IReadOnlyCollection<string?> itemIds,
        string? nextPageToken,
        int? totalResults)
    {
        PagesFetched++;
        if (totalResults.HasValue)
        {
            TotalResults = totalResults;
        }

        var ids = itemIds.Where(id => !string.IsNullOrEmpty(id)).Select(id => id!).ToList();
        var allSeen = ids.Count > 0 && ids.Count == itemIds.Count && ids.All(_seenIds.Contains);
        _seenIds.UnionWith(ids);

        if (string.IsNullOrEmpty(nextPageToken))
        {
            return YouTubePageWalkStopReason.None;
        }

        if (!string.IsNullOrEmpty(requestedPageToken) &&
            string.Equals(requestedPageToken, nextPageToken, StringComparison.Ordinal))
        {
            return YouTubePageWalkStopReason.RepeatedPageToken;
        }

        if (itemIds.Count == 0)
        {
            return YouTubePageWalkStopReason.EmptyPage;
        }

        if (allSeen)
        {
            return YouTubePageWalkStopReason.NoNewItems;
        }

        if (PageCap is { } cap && PagesFetched >= cap)
        {
            return YouTubePageWalkStopReason.PageCapExceeded;
        }

        return YouTubePageWalkStopReason.None;
    }

    /// <summary>
    /// True for stop reasons that point at a YouTube paging fault worth an operator's attention.
    /// <see cref="YouTubePageWalkStopReason.EmptyPage"/> is a tidy end of a finished walk.
    /// </summary>
    public static bool IsError(YouTubePageWalkStopReason reason) =>
        reason is YouTubePageWalkStopReason.RepeatedPageToken
            or YouTubePageWalkStopReason.NoNewItems
            or YouTubePageWalkStopReason.PageCapExceeded;

    /// <summary>
    /// Logs a stop at Error for <see cref="IsError"/> reasons and at Information for
    /// <see cref="YouTubePageWalkStopReason.EmptyPage"/>. The page token is not logged.
    /// </summary>
    public void LogStop(
        ILogger logger,
        YouTubePageWalkStopReason reason,
        string walk,
        string walkId,
        string? source,
        string? sourceIdentifier,
        int itemsCollected)
    {
        if (reason == YouTubePageWalkStopReason.None)
        {
            return;
        }

        logger.Log(
            IsError(reason) ? LogLevel.Error : LogLevel.Information,
            StoppedMessageTemplate,
            reason,
            walk,
            walkId,
            source,
            sourceIdentifier,
            PagesFetched,
            TotalResults,
            PageSize,
            itemsCollected);
    }
}
