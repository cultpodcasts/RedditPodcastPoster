using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.EdgeApi.Clients;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public interface ID1SearchIndexWriter
{
    /// <summary>The sync is enabled and fully configured. Callers skip building documents when false.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Sends the upserts and deletes to the production D1 search index. <b>Never throws</b> for a D1, network or Auth0 failure:
    /// the outcome is logged and returned, so a D1 problem can never break Azure indexing or a curator request.
    /// Only cancellation of <paramref name="cancellationToken"/> propagates.
    /// </summary>
    Task<D1SearchWriteResult> Write(
        IReadOnlyCollection<D1SearchDocument> upserts,
        IReadOnlyCollection<string> deletes,
        CancellationToken cancellationToken);
}

/// <summary>
/// Writes to the production D1 search index through the api Worker's <c>POST /search/admin/docs</c> (Api SP-1.4):
/// at most 100 upserts and 1000 deletes per request, each id once, one D1 transaction per request. Writes are
/// idempotent (the Worker's <c>modifiedAt</c> guard), so a failed request is retried once and a failed run is
/// simply repeated by the next drip. Failures are logged at Error with the status and the Worker's
/// message (never the token).
/// </summary>
public sealed class D1SearchIndexWriter(
    IHttpClientFactory httpClientFactory,
    ID1SearchTokenSource tokenSource,
    IOptions<D1SearchOptions> options,
    TimeProvider timeProvider,
    ILogger<D1SearchIndexWriter> logger) : ID1SearchIndexWriter
{
    public const string HttpClientName = "d1-search";
    public const int MaxUpsertsPerRequest = 100;
    public const int MaxDeletesPerRequest = 1000;
    private const string Route = "search/admin/docs";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly D1SearchOptions _options = options.Value;
    private int _loggedUnusable;

    public bool IsActive => _options.IsUsable;

    public async Task<D1SearchWriteResult> Write(
        IReadOnlyCollection<D1SearchDocument> upserts,
        IReadOnlyCollection<string> deletes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upserts);
        ArgumentNullException.ThrowIfNull(deletes);
        if (!IsActive)
        {
            LogUnusableOnce();
            return D1SearchWriteResult.Skipped;
        }

        var (upsertList, deleteList) = Normalise(upserts, deletes);
        if (upsertList.Count == 0 && deleteList.Count == 0)
        {
            return D1SearchWriteResult.Skipped;
        }

        return await Send(upsertList, deleteList, cancellationToken);
    }

    private async Task<D1SearchWriteResult> Send(
        IReadOnlyList<D1SearchDocument> upserts,
        IReadOnlyList<string> deletes,
        CancellationToken cancellationToken)
    {
        int upserted = 0, deleted = 0, stale = 0, notFound = 0;
        var requests = Math.Max(
            (upserts.Count + MaxUpsertsPerRequest - 1) / MaxUpsertsPerRequest,
            (deletes.Count + MaxDeletesPerRequest - 1) / MaxDeletesPerRequest);
        for (var i = 0; i < requests; i++)
        {
            var body = new AdminDocsRequest(
                upserts.Skip(i * MaxUpsertsPerRequest).Take(MaxUpsertsPerRequest).ToArray(),
                deletes.Skip(i * MaxDeletesPerRequest).Take(MaxDeletesPerRequest).ToArray());
            try
            {
                var outcome = await SendWithRetry(body, cancellationToken);
                upserted += outcome.Upserted;
                deleted += outcome.Deleted;
                stale += outcome.Stale?.Length ?? 0;
                notFound += outcome.NotFound?.Length ?? 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // Later chunks would most likely fail the same way; the next drip replays the lot (idempotent).
                logger.LogError(ex,
                    "D1 search write failed on request {Request} of {Requests} " +
                    "({Upserts} upsert(s), {Deletes} delete(s) in this call). Azure indexing is unaffected; the next drip retries. {Message}",
                    i + 1, requests, upserts.Count, deletes.Count, ex.Message);
                return new D1SearchWriteResult(true, false, upserted, deleted, stale, notFound, ex.Message);
            }
        }

        logger.LogInformation(
            "D1 search write: upserted {Upserted}, deleted {Deleted}, stale {Stale}, not-found {NotFound} ({Requests} request(s)).",
            upserted, deleted, stale, notFound, requests);
        return new D1SearchWriteResult(true, true, upserted, deleted, stale, notFound, null);
    }

    private async Task<AdminDocsOutcome> SendWithRetry(AdminDocsRequest body, CancellationToken cancellationToken)
    {
        try
        {
            return await SendOnce(body, cancellationToken);
        }
        catch (Exception ex) when (IsRetryable(ex, cancellationToken))
        {
            logger.LogWarning("D1 search write failed ({Message}); retrying once.", ex.Message);
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, _options.RetryDelayMilliseconds)), timeProvider, cancellationToken);
            return await SendOnce(body, cancellationToken);
        }
    }

    private async Task<AdminDocsOutcome> SendOnce(AdminDocsRequest body, CancellationToken cancellationToken)
    {
        var token = await tokenSource.GetToken(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.Endpoint!, Route));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(body, options: Json);
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                tokenSource.Invalidate();
            }

            var challenged = CloudflareChallengeResponse.LooksLikeBotChallenge(response.StatusCode, text);
            throw new D1SearchHttpException(response.StatusCode, Describe(response.StatusCode, text, challenged), challenged);
        }

        return JsonSerializer.Deserialize<AdminDocsOutcome>(text, Json)
               ?? throw new D1SearchWriteException("The search index answered 200 with no outcome.");
    }

    private static string Describe(HttpStatusCode status, string body, bool challenged)
    {
        var hint = challenged
            ? " (Cloudflare Bot Fight challenge, the Worker never saw it: d1Search:Endpoint must be the api Worker's " +
              "bypass host, Key Vault Api-Endpoint)"
            : status switch
            {
                HttpStatusCode.Unauthorized => " (token rejected: wrong tenant or audience for this api Worker?)",
                HttpStatusCode.Forbidden =>
                    " (no write:search-index grant on this client? see the api Worker's search_admin.forbidden log)",
                _ => string.Empty
            };
        return $"HTTP {(int)status}{hint}: {CloudflareChallengeResponse.TruncateBody(body)}";
    }

    private static bool IsRetryable(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        // A Cloudflare challenge repeats on retry, whatever its status.
        D1SearchHttpException { IsCloudflareChallenge: true } => false,
        D1SearchHttpException http => http.StatusCode is HttpStatusCode.TooManyRequests || (int)http.StatusCode >= 500,
        HttpRequestException => true,
        // HttpClient timeout surfaces as TaskCanceledException without our token being cancelled.
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        _ => false
    };

    /// <summary>
    /// The Worker rejects a request that names an id twice. The last upsert of an id wins; an id both upserted
    /// and deleted is upserted (the caller decided it is in the index); deletes are de-duplicated.
    /// </summary>
    public static (IReadOnlyList<D1SearchDocument> Upserts, IReadOnlyList<string> Deletes) Normalise(
        IEnumerable<D1SearchDocument> upserts,
        IEnumerable<string> deletes)
    {
        var byId = new Dictionary<string, D1SearchDocument>(StringComparer.Ordinal);
        foreach (var doc in upserts)
        {
            byId[doc.Id] = doc;
        }

        var deleteList = deletes
            .Where(id => !string.IsNullOrWhiteSpace(id) && !byId.ContainsKey(id))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return (byId.Values.ToArray(), deleteList);
    }

    private void LogUnusableOnce()
    {
        if (!_options.Enabled || Interlocked.Exchange(ref _loggedUnusable, 1) == 1)
        {
            return;
        }

        logger.LogWarning(
            "D1 search sync is enabled but has no endpoint or Auth0 credentials; nothing is sent " +
            "(RPP #1460: provision exports them from HCP Terraform).");
    }

    private sealed record AdminDocsRequest(
        [property: JsonPropertyName("upserts")] D1SearchDocument[] Upserts,
        [property: JsonPropertyName("deletes")] string[] Deletes);

    private sealed record AdminDocsOutcome(
        [property: JsonPropertyName("upserted")] int Upserted,
        [property: JsonPropertyName("deleted")] int Deleted,
        [property: JsonPropertyName("stale")] string[]? Stale,
        [property: JsonPropertyName("notFound")] string[]? NotFound);
}

public sealed class D1SearchHttpException(HttpStatusCode statusCode, string message, bool isCloudflareChallenge = false)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;

    /// <summary>The response was Cloudflare challenge HTML (Bot Fight Mode), not the api Worker's answer.</summary>
    public bool IsCloudflareChallenge { get; } = isCloudflareChallenge;
}

/// <param name="Ran">The sync is active and there was something to send.</param>
/// <param name="Succeeded">Every request succeeded (true when nothing ran).</param>
/// <param name="Error">Why the write failed; null on success.</param>
public sealed record D1SearchWriteResult(
    bool Ran,
    bool Succeeded,
    int Upserted,
    int Deleted,
    int Stale,
    int NotFound,
    string? Error)
{
    /// <summary>Nothing sent: the sync is off or unconfigured, or the call had nothing to write.</summary>
    public static D1SearchWriteResult Skipped { get; } = new(false, true, 0, 0, 0, 0, null);
}
