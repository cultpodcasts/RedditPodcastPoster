using System.Net;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// The api Worker's <c>/search/admin/rebuild</c> routes (Api SP-2.3, #226): rebuild the D1 search index into shadow
/// tables and swap it in with one transaction. Same host, token and permission (<c>write:search-index</c>) as the
/// write path. Only <see cref="Load"/> is retried (its upserts are idempotent); every other action changes state, so a
/// failure is reported and <see cref="D1SearchRebuilder"/> reads <see cref="Status"/> to find out what happened.
/// Throws <see cref="D1SearchHttpException"/> (409 when the rebuild's state does not allow the action, or the swap
/// failed verification) or <see cref="D1SearchWriteException"/>.
/// </summary>
public interface ID1SearchRebuildClient
{
    Task<D1SearchRebuildStatus> Status(CancellationToken cancellationToken);

    /// <summary>Creates the shadow tables; returns the rebuild id every later call names.</summary>
    Task<string> Start(CancellationToken cancellationToken);

    /// <summary>At most <see cref="D1SearchIndexWriter.MaxUpsertsPerRequest"/> documents, each id once.</summary>
    Task<D1SearchRebuildLoadOutcome> Load(string rebuildId, IReadOnlyList<D1SearchDocument> documents, CancellationToken cancellationToken);

    /// <summary>Verifies the shadow against <paramref name="expected"/> and swaps it in; 409 when verification fails.</summary>
    Task Swap(string rebuildId, D1SearchRebuildExpectation expected, CancellationToken cancellationToken);

    Task<D1SearchRebuildCheck> Check(string rebuildId, IReadOnlyList<string> sampleQueries, CancellationToken cancellationToken);

    /// <summary>Drops the parked previous index.</summary>
    Task Finish(string rebuildId, CancellationToken cancellationToken);

    /// <summary>Puts the parked previous index back.</summary>
    Task SwapBack(string rebuildId, CancellationToken cancellationToken);

    /// <summary>Drops the shadow (while loading, or after a swap-back).</summary>
    Task Abort(string rebuildId, CancellationToken cancellationToken);
}

public sealed record D1SearchRebuildCounts(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("kinds")] IReadOnlyDictionary<string, int>? Kinds);

/// <param name="State"><c>none</c>, <c>loading</c>, <c>swapped</c> or <c>swappedBack</c>.</param>
public sealed record D1SearchRebuildStatus(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("rebuildId")] string? RebuildId,
    [property: JsonPropertyName("live")] D1SearchRebuildCounts? Live,
    [property: JsonPropertyName("shadow")] D1SearchRebuildCounts? Shadow,
    [property: JsonPropertyName("parked")] D1SearchRebuildCounts? Parked)
{
    public const string None = "none";
    public const string Loading = "loading";
    public const string Swapped = "swapped";
    public const string SwappedBack = "swappedBack";
}

public sealed record D1SearchRebuildLoadOutcome(
    [property: JsonPropertyName("upserted")] int Upserted,
    [property: JsonPropertyName("stale")] string[]? Stale);

/// <param name="Tolerance">Allowed difference per count: pushes made to the index during the load also land in the shadow.</param>
/// <param name="MaxShrink">Refuse when the new index is smaller than the live one by more than this share.</param>
public sealed record D1SearchRebuildExpectation(
    [property: JsonPropertyName("expectedTotal")] int ExpectedTotal,
    [property: JsonPropertyName("expectedKinds")] IReadOnlyDictionary<string, int> ExpectedKinds,
    [property: JsonPropertyName("tolerance")] int Tolerance,
    [property: JsonPropertyName("maxShrink")] double MaxShrink);

public sealed record D1SearchRebuildIntegrity(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("error")] string? Error);

public sealed record D1SearchRebuildSample(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("hits")] int Hits);

/// <param name="Ok">The full-text index matches the new live table and every sample query found something.</param>
public sealed record D1SearchRebuildCheck(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("integrity")] D1SearchRebuildIntegrity? Integrity,
    [property: JsonPropertyName("counts")] D1SearchRebuildCounts? Counts,
    [property: JsonPropertyName("samples")] D1SearchRebuildSample[]? Samples);

public sealed partial class D1SearchIndexWriter
{
    private const string RebuildRoute = "search/admin/rebuild";

    public async Task<D1SearchRebuildStatus> Status(CancellationToken cancellationToken)
    {
        RequireActive();
        return await WithRetry(() => SendOnce<D1SearchRebuildStatus>(HttpMethod.Get, RebuildRoute, null, cancellationToken),
            cancellationToken);
    }

    public async Task<string> Start(CancellationToken cancellationToken)
    {
        RequireActive();
        var started = await SendOnce<RebuildIdBody>(HttpMethod.Post, $"{RebuildRoute}/start", new { }, cancellationToken);
        return started.RebuildId ?? throw new D1SearchWriteException("The search index started a rebuild with no id.");
    }

    public async Task<D1SearchRebuildLoadOutcome> Load(
        string rebuildId,
        IReadOnlyList<D1SearchDocument> documents,
        CancellationToken cancellationToken)
    {
        RequireActive();
        if (documents.Count > MaxUpsertsPerRequest)
        {
            throw new ArgumentException($"At most {MaxUpsertsPerRequest} documents per load request.", nameof(documents));
        }

        var body = new LoadBody(rebuildId, documents.ToArray());
        return await WithRetry(
            () => SendOnce<D1SearchRebuildLoadOutcome>(HttpMethod.Post, $"{RebuildRoute}/docs", body, cancellationToken),
            cancellationToken);
    }

    public async Task Swap(string rebuildId, D1SearchRebuildExpectation expected, CancellationToken cancellationToken)
    {
        RequireActive();
        var body = new SwapBody(rebuildId, expected.ExpectedTotal, expected.ExpectedKinds, expected.Tolerance, expected.MaxShrink);
        await SendOnce<RebuildIdBody>(HttpMethod.Post, $"{RebuildRoute}/swap", body, cancellationToken);
    }

    public async Task<D1SearchRebuildCheck> Check(
        string rebuildId,
        IReadOnlyList<string> sampleQueries,
        CancellationToken cancellationToken)
    {
        RequireActive();
        var body = new CheckBody(rebuildId, sampleQueries.ToArray());
        return await WithRetry(
            () => SendOnce<D1SearchRebuildCheck>(HttpMethod.Post, $"{RebuildRoute}/check", body, cancellationToken),
            cancellationToken);
    }

    public Task Finish(string rebuildId, CancellationToken cancellationToken) => Act("finish", rebuildId, cancellationToken);

    public Task SwapBack(string rebuildId, CancellationToken cancellationToken) => Act("swap-back", rebuildId, cancellationToken);

    public Task Abort(string rebuildId, CancellationToken cancellationToken) => Act("abort", rebuildId, cancellationToken);

    private async Task Act(string action, string rebuildId, CancellationToken cancellationToken)
    {
        RequireActive();
        await SendOnce<RebuildIdBody>(HttpMethod.Post, $"{RebuildRoute}/{action}", new RebuildIdBody(rebuildId), cancellationToken);
    }

    private void RequireActive()
    {
        if (!IsActive)
        {
            throw new D1SearchWriteException("The D1 search sync is not configured.");
        }
    }

    /// <summary>A 409 from the rebuild routes: the state does not allow the action, or the swap failed verification.</summary>
    public static bool IsRebuildConflict(Exception ex) => ex is D1SearchHttpException { StatusCode: HttpStatusCode.Conflict };

    private sealed record RebuildIdBody([property: JsonPropertyName("rebuildId")] string? RebuildId);

    private sealed record LoadBody(
        [property: JsonPropertyName("rebuildId")] string RebuildId,
        [property: JsonPropertyName("upserts")] D1SearchDocument[] Upserts);

    private sealed record SwapBody(
        [property: JsonPropertyName("rebuildId")] string RebuildId,
        [property: JsonPropertyName("expectedTotal")] int ExpectedTotal,
        [property: JsonPropertyName("expectedKinds")] IReadOnlyDictionary<string, int> ExpectedKinds,
        [property: JsonPropertyName("tolerance")] int Tolerance,
        [property: JsonPropertyName("maxShrink")] double MaxShrink);

    private sealed record CheckBody(
        [property: JsonPropertyName("rebuildId")] string RebuildId,
        [property: JsonPropertyName("sampleQueries")] string[] SampleQueries);
}
