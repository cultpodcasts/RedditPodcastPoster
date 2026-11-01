using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

/// <summary>
/// X API v2 client. A typed <see cref="HttpClient"/> from <c>IHttpClientFactory</c> (base address
/// <see cref="TwitterOptions.ApiBaseAddress"/>, timeout <see cref="TwitterOptions.RequestTimeoutSeconds"/>);
/// <see cref="Auth.TwitterAuthenticationHandler"/> signs each request. Post creation is never retried
/// automatically, so a post X accepted is not sent twice by this client.
/// </summary>
public class TwitterClient(
    HttpClient httpClient,
    IOptions<TwitterOptions> options,
    ILogger<TwitterClient> logger
) : ITwitterClient
{
    private readonly TwitterOptions _options = options.Value;

    public async Task<PostTweetResponse> Send(string tweet, CancellationToken cancellationToken = default)
    {
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
            return new PostTweetResponse(TweetSendStatus.Sent);
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

        if (response.StatusCode == HttpStatusCode.TooManyRequests || problem.IsRateOrUsageLimited)
        {
            var retryAfterHeaders = response.Headers.Where(x => x.Key.StartsWith("x-rate-limit"))
                .Select(x => new {name = x.Key, value = x.Value.FirstOrDefault()}).ToArray();

            var resetDetails = "";
            var reset = retryAfterHeaders.FirstOrDefault(x => x.name == "x-rate-limit-reset")?.value;
            if (long.TryParse(reset, out var rateLimitReset))
            {
                var resetAt = DateTimeOffset.FromUnixTimeSeconds(rateLimitReset);
                resetDetails = $"Reset-At: '{resetAt:O}'. ";
            }

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

    public async Task<GetTweetsResponseWrapper> GetTweets(CancellationToken cancellationToken = default)
    {
        using var getTweetsRequest = new HttpRequestMessage(HttpMethod.Get,
            $"2/users/{_options.TwitterId}/tweets?tweet.fields=entities");

        using var response = await httpClient.SendAsync(getTweetsRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var tweetsResponse = await response.Content.ReadFromJsonAsync<GetTweetsResponse>(cancellationToken);
            if (tweetsResponse?.Tweets == null)
            {
                logger.LogError("Twitter GetTweets returned success but response body or tweets was null.");
                return new GetTweetsResponseWrapper(GetTweetsState.Other);
            }

            return new GetTweetsResponseWrapper(GetTweetsState.Retrieved, tweetsResponse.Tweets);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new GetTweetsResponseWrapper(GetTweetsState.TooManyRequests);
        }

        var problem = XApiProblem.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        logger.LogError("Twitter GetTweets failed. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}'.",
            response.StatusCode, problem.Summary());
        return new GetTweetsResponseWrapper(GetTweetsState.Other);
    }

    public async Task<bool> DeleteTweet(Tweet tweet, CancellationToken cancellationToken = default)
    {
        using var deleteTweetRequest = new HttpRequestMessage(HttpMethod.Delete, $"2/tweets/{tweet.Id}");

        using var response = await httpClient.SendAsync(deleteTweetRequest, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Deleted tweet with id '{TweetId}'.", tweet.Id);
            return true;
        }

        var problem = XApiProblem.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        logger.LogError("Failed to delete tweet with id '{TweetId}'. Status-code: '{ResponseStatusCode}'. Problem: '{Problem}'.",
            tweet.Id, response.StatusCode, problem.Summary());
        return false;
    }
}
