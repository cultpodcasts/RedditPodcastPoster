using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Social;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// X API v2 client. A typed <see cref="HttpClient"/> from <c>IHttpClientFactory</c> (base address
/// <see cref="TwitterOptions.ApiBaseAddress"/>, timeout <see cref="TwitterOptions.RequestTimeoutSeconds"/>);
/// <see cref="Auth.TwitterAuthenticationHandler"/> signs each request. Post creation is never retried
/// automatically, so a post X accepted is not sent twice by this client. Rate-limit and usage-cap windows
/// are persisted in LookUps (<see cref="IXApiRateLimitStore"/>) so recycled Function workers skip X until
/// <c>x-rate-limit-reset</c> (or the usage-cap skip-until) elapses.
/// </summary>
public class TwitterClient(
    HttpClient httpClient,
    IOptions<TwitterOptions> options,
    IXApiRateLimitStore rateLimitStore,
    TimeProvider timeProvider,
    ILogger<TwitterClient> logger
) : ITwitterClient
{
    private readonly TwitterOptions _options = options.Value;

    public async Task<PostTweetResponse> Send(string tweet, CancellationToken cancellationToken = default)
    {
        var skip = await TryGetActiveSkip(cancellationToken);
        if (skip == XApiSkipReason.UsageCapped)
        {
            logger.LogWarning(
                "Skipping tweet send: X usage is capped until the LookUps skip window elapses. Tweet: '{Tweet}'.",
                tweet);
            return new PostTweetResponse(TweetSendStatus.PaymentRequired, tweet);
        }

        if (skip == XApiSkipReason.RateLimited)
        {
            logger.LogWarning(
                "Skipping tweet send: X rate limit has not reset yet (LookUps do-not-retry-before). Tweet: '{Tweet}'.",
                tweet);
            return new PostTweetResponse(TweetSendStatus.TooManyRequests, tweet);
        }

        var tweetData = new {text = tweet};
        var jsonData = JsonSerializer.Serialize(tweetData);

        using var createTweetRequest = new HttpRequestMessage(HttpMethod.Post, "2/tweets")
        {
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        using var response = await httpClient.SendAsync(createTweetRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Tweet sent successfully! Tweet: '{Tweet}'.", tweet);
            await ClearSkipWindow(cancellationToken);
            return new PostTweetResponse(TweetSendStatus.Sent, TweetId: await ReadPostedTweetId(response));
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var problem = XApiProblem.Parse(body);

        if (response.StatusCode == HttpStatusCode.Forbidden && problem.IsDuplicateContent)
        {
            logger.LogError(
                "Failed to send tweet. Duplicate-tweet. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}', Tweet: '{Tweet}'.",
                response.ReasonPhrase, response.StatusCode, problem.Summary(), tweet);
            return new PostTweetResponse(TweetSendStatus.DuplicateForbidden, tweetData.text);
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired || problem.IsUsageCapped)
        {
            await PersistUsageCap(cancellationToken);
            logger.LogError(
                "Failed to send tweet. Usage-capped / out-of-credits. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}', Tweet: '{Tweet}'.",
                response.ReasonPhrase, response.StatusCode, problem.Summary(), tweet);
            // Usage-capped and 402 both mean credits are gone; PaymentRequired stops the run with the credits log line.
            return new PostTweetResponse(TweetSendStatus.PaymentRequired, tweetData.text);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests || problem.IsRateOrUsageLimited)
        {
            var resetUtc = XApiRateLimitPolicy.TryParseResetUtc(
                response.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value)));
            await PersistRateLimit(resetUtc, cancellationToken);

            var resetDetails = resetUtc.HasValue ? $"Reset-At: '{resetUtc.Value:O}'. " : "";
            var retryAfterHeaders = response.Headers.Where(x => x.Key.StartsWith("x-rate-limit", StringComparison.OrdinalIgnoreCase))
                .Select(x => new {name = x.Key, value = x.Value.FirstOrDefault()}).ToArray();

            logger.LogError(
                "Failed to send tweet. Too-many-requests. {ResetDetails}Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Headers: {Serialize} Problem: '{Problem}', Tweet: '{Tweet}'.",
                resetDetails, response.ReasonPhrase, response.StatusCode, JsonSerializer.Serialize(retryAfterHeaders),
                problem.Summary(), tweet);
            return new PostTweetResponse(TweetSendStatus.TooManyRequests, tweetData.text);
        }

        logger.LogError(
            "Failed to send tweet. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}', Tweet: '{Tweet}'.",
            response.ReasonPhrase, response.StatusCode, problem.Summary(), tweet);

        return new PostTweetResponse(TweetSendFailureClassifier.Classify(response.StatusCode), tweetData.text);
    }

    public async Task<GetTweetsResponseWrapper> GetTimelineSince(DateTimeOffset startTime, DateTimeOffset? endTime,
        string? paginationToken, CancellationToken cancellationToken = default)
    {
        var skip = await TryGetActiveSkip(cancellationToken);
        if (skip is not null)
        {
            logger.LogWarning(
                "Skipping GetTimelineSince (since '{StartTime:O}'): X is blocked ({Skip}) until the LookUps skip window elapses.",
                startTime, skip);
            return new GetTweetsResponseWrapper(GetTweetsState.TooManyRequests);
        }

        var query = new StringBuilder(
            $"2/users/{_options.TwitterId}/tweets?tweet.fields=entities&max_results={TimelinePageSize}");
        query.Append("&start_time=").Append(Uri.EscapeDataString(ToXTime(startTime)));
        if (endTime.HasValue)
        {
            query.Append("&end_time=").Append(Uri.EscapeDataString(ToXTime(endTime.Value)));
        }

        if (!string.IsNullOrWhiteSpace(paginationToken))
        {
            query.Append("&pagination_token=").Append(Uri.EscapeDataString(paginationToken));
        }

        using var getTweetsRequest = new HttpRequestMessage(HttpMethod.Get, query.ToString());
        using var response = await httpClient.SendAsync(getTweetsRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var tweetsResponse = await response.Content.ReadFromJsonAsync<GetTweetsResponse>(cancellationToken);
            if (tweetsResponse == null)
            {
                logger.LogError("Twitter GetTimelineSince (since '{StartTime:O}') returned success but no body.", startTime);
                return new GetTweetsResponseWrapper(GetTweetsState.Other);
            }

            await ClearSkipWindow(cancellationToken);

            // An empty window has no "data"; that is a complete, empty page rather than an error.
            var nextToken = string.IsNullOrWhiteSpace(tweetsResponse.MetaData?.NextToken)
                ? null
                : tweetsResponse.MetaData.NextToken;
            return new GetTweetsResponseWrapper(GetTweetsState.Retrieved, tweetsResponse.Tweets ?? [], nextToken);
        }

        var problem = XApiProblem.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (problem.IsLimitOrUsageResponse(response.StatusCode))
        {
            await PersistLimitFromResponse(response, problem, cancellationToken);
            var resetUtc = XApiRateLimitPolicy.TryParseResetUtc(
                response.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value)));
            logger.LogError(
                "Twitter GetTimelineSince (since '{StartTime:O}') rate-limited or usage-capped. Reset-At: '{ResetAt}'. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}'.",
                startTime, resetUtc?.ToString("O") ?? "(none)", response.StatusCode, problem.Summary());
            return new GetTweetsResponseWrapper(GetTweetsState.TooManyRequests);
        }

        logger.LogError(
            "Twitter GetTimelineSince (since '{StartTime:O}') failed. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}'.",
            startTime, response.StatusCode, problem.Summary());
        return new GetTweetsResponseWrapper(GetTweetsState.Other);
    }

    private static string ToXTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    public async Task<DeleteTweetState> DeleteTweetById(string tweetId, CancellationToken cancellationToken = default)
    {
        if (!TweetResponseReader.IsTweetId(tweetId))
        {
            logger.LogError("Not deleting tweet: '{TweetId}' is not an X post id.", tweetId);
            return DeleteTweetState.Failed;
        }

        var skip = await TryGetActiveSkip(cancellationToken);
        if (skip is not null)
        {
            logger.LogWarning(
                "Skipping delete of tweet '{TweetId}': X is blocked ({Skip}) until the LookUps skip window elapses.",
                tweetId, skip);
            return DeleteTweetState.TooManyRequests;
        }

        using var deleteTweetRequest = new HttpRequestMessage(HttpMethod.Delete, $"2/tweets/{tweetId}");
        using var response = await httpClient.SendAsync(deleteTweetRequest, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var problem = XApiProblem.Parse(body);
        if (problem.IsLimitOrUsageResponse(response.StatusCode))
        {
            await PersistLimitFromResponse(response, problem, cancellationToken);
        }
        else if (response.IsSuccessStatusCode)
        {
            await ClearSkipWindow(cancellationToken);
        }

        var state = TweetResponseReader.ReadDeleteState(response.StatusCode, body);
        if (state == DeleteTweetState.Deleted)
        {
            logger.LogInformation("Deleted tweet with id '{TweetId}'.", tweetId);
        }
        else
        {
            logger.LogWarning("Delete of tweet with id '{TweetId}': {State}. Status-code: '{ResponseStatusCode}'.",
                tweetId, state, response.StatusCode);
        }

        return state;
    }

    /// <summary>X's maximum page size for the user posts timeline.</summary>
    private const int TimelinePageSize = 100;

    private async Task<XApiSkipReason?> TryGetActiveSkip(CancellationToken cancellationToken)
    {
        try
        {
            var state = await rateLimitStore.GetAsync(cancellationToken);
            return XApiRateLimitPolicy.GetActiveSkip(state, timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (Exception ex)
        {
            // Prefer attempting the call over blocking the hourly run when LookUps is unreachable.
            logger.LogWarning(ex, "Could not read X rate-limit LookUps state; proceeding without a skip window.");
            return null;
        }
    }

    private async Task PersistLimitFromResponse(
        HttpResponseMessage response,
        XApiProblem problem,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.PaymentRequired || problem.IsUsageCapped)
        {
            await PersistUsageCap(cancellationToken);
            return;
        }

        var resetUtc = XApiRateLimitPolicy.TryParseResetUtc(
            response.Headers.Select(h => new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value)));
        await PersistRateLimit(resetUtc, cancellationToken);
    }

    private async Task PersistRateLimit(DateTime? resetUtc, CancellationToken cancellationToken)
    {
        await MutateState(state => XApiRateLimitPolicy.ApplyRateLimit(state, resetUtc, timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
    }

    private async Task PersistUsageCap(CancellationToken cancellationToken)
    {
        await MutateState(state => XApiRateLimitPolicy.ApplyUsageCap(state, timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
    }

    private async Task ClearSkipWindow(CancellationToken cancellationToken)
    {
        try
        {
            var existing = await rateLimitStore.GetAsync(cancellationToken);
            if (existing is null ||
                (!existing.RateLimitResetUtc.HasValue && !existing.UsageCappedUntilUtc.HasValue))
            {
                return;
            }

            XApiRateLimitPolicy.Clear(existing, timeProvider.GetUtcNow().UtcDateTime);
            await rateLimitStore.SaveAsync(existing, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not clear X rate-limit LookUps state after a successful call.");
        }
    }

    private async Task MutateState(Action<XApiRateLimitState> mutate, CancellationToken cancellationToken)
    {
        try
        {
            var state = await rateLimitStore.GetAsync(cancellationToken) ?? new XApiRateLimitState();
            mutate(state);
            await rateLimitStore.SaveAsync(state, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not persist X rate-limit LookUps state; this Function instance may retry X sooner than intended.");
        }
    }

    private async Task<string?> ReadPostedTweetId(HttpResponseMessage response)
    {
        string? body;
        try
        {
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tweet was sent but its response body could not be read; tweet id not stored.");
            return null;
        }

        var tweetId = TweetResponseReader.ReadPostedTweetId(body);
        if (tweetId == null)
        {
            logger.LogWarning("Tweet was sent but the response had no post id; tweet id not stored.");
        }

        return tweetId;
    }
}
