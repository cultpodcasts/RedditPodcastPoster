using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices.YouTube.Paging;

/// <summary>Why a YouTube page walk stopped before the API stopped returning a next-page token.</summary>
public enum YouTubePageWalkStopReason
{
    /// <summary>Keep walking (or the walk ended normally because no next-page token was returned).</summary>
    None,

    /// <summary>The response's next-page token equals the token just requested: following it would loop forever.</summary>
    RepeatedPageToken,

    /// <summary>
    /// A page returned zero items but still carried a new next-page token, and the walk has already
    /// seen at least <c>totalResults - 2</c> distinct ids (or YouTube gave no total).
    /// </summary>
    EmptyPage,

    /// <summary>Every item on the page had already been seen in this walk.</summary>
    NoNewItems,

    /// <summary>
    /// The walk reached <c>ceil(totalResults / pageSize) + 2</c> pages and YouTube still returned a next-page token.
    /// </summary>
    PageCapExceeded,

    /// <summary>
    /// <see cref="YouTubePageWalkGuard.MaxConsecutiveEmptyPages"/> pages in a row returned zero items with a new
    /// next-page token while the walk was still short of <c>totalResults</c>. Without this the chain is bounded only by
    /// the page cap, which grows with an over-reported total (about 100 calls for a total of 5,000).
    /// </summary>
    ConsecutiveEmptyPages
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

    /// <summary>
    /// Empty pages in a row (each with a new token, short of the total) before the walk stops. One empty page followed
    /// by real items still walks on.
    /// </summary>
    public const int MaxConsecutiveEmptyPages = 2;

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

    /// <summary>Distinct non-empty item ids seen so far in this walk.</summary>
    public int DistinctIdsSeen => _seenIds.Count;

    /// <summary>Item count of the most recent page.</summary>
    public int LastPageItemCount { get; private set; }

    /// <summary>Pages in a row, ending with the latest, that returned zero items.</summary>
    public int ConsecutiveEmptyPages { get; private set; }

    /// <summary>
    /// True when the walk has read (nearly) everything YouTube says the list holds: at least
    /// <c>totalResults - 2</c> distinct ids, or no total was reported. The slack covers items
    /// YouTube counts but does not return (e.g. the 143-of-144 Kelly R. Minter playlist).
    /// </summary>
    public bool ReadToEnd => TotalResults is not { } total || DistinctIdsSeen >= total - PageCapSlack;

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
        LastPageItemCount = itemIds.Count;
        ConsecutiveEmptyPages = itemIds.Count == 0 ? ConsecutiveEmptyPages + 1 : 0;
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
            // An empty page with a new token is not a loop. Only stop once the walk has read to the
            // end; when clearly short, keep following it and let the cap or the repeated-token check end it.
            if (ReadToEnd)
            {
                return YouTubePageWalkStopReason.EmptyPage;
            }

            if (ConsecutiveEmptyPages >= MaxConsecutiveEmptyPages)
            {
                return YouTubePageWalkStopReason.ConsecutiveEmptyPages;
            }
        }
        else if (allSeen)
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
    /// The log level for a stop.
    /// <list type="bullet">
    /// <item><see cref="YouTubePageWalkStopReason.EmptyPage"/>: Information (a tidy end of a finished walk).</item>
    /// <item><see cref="YouTubePageWalkStopReason.RepeatedPageToken"/> on an empty page after <see cref="ReadToEnd"/>:
    /// Warning. This is the Kelly R. Minter shape: every item was read and YouTube's last token points back at itself.</item>
    /// <item>Otherwise Error, because the walk stopped clearly short of <c>totalResults</c> or YouTube replayed or
    /// over-served pages, so items may be missing.</item>
    /// </list>
    /// </summary>
    public LogLevel LevelFor(YouTubePageWalkStopReason reason) => reason switch
    {
        YouTubePageWalkStopReason.None => LogLevel.None,
        YouTubePageWalkStopReason.EmptyPage => LogLevel.Information,
        YouTubePageWalkStopReason.RepeatedPageToken when LastPageItemCount == 0 && ReadToEnd => LogLevel.Warning,
        _ => LogLevel.Error
    };

    /// <summary>
    /// Logs a stop at <see cref="LevelFor"/> (or <paramref name="level"/> when given). The page token is not logged.
    /// </summary>
    public void LogStop(
        ILogger logger,
        YouTubePageWalkStopReason reason,
        string walk,
        string walkId,
        string? source,
        string? sourceIdentifier,
        int itemsCollected,
        LogLevel? level = null)
    {
        if (reason == YouTubePageWalkStopReason.None)
        {
            return;
        }

        logger.Log(
            level ?? LevelFor(reason),
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
