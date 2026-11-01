using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OAuth;
using RedditPodcastPoster.Twitter.Configuration;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter.Clients;

public class TwitterClient(
    IOptions<TwitterOptions> options,
    ILogger<TwitterClient> logger
) : ITwitterClient
{
    private readonly TwitterOptions _options = options.Value;

    public async Task<PostTweetResponse> Send(string tweet)
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var tweetData = new {text = tweet};
        var jsonData = JsonSerializer.Serialize(tweetData);

        var createTweetRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/tweets")
        {
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        using var httpClient = new HttpClient(oauth);

        using var response = await httpClient.SendAsync(createTweetRequest);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Tweet sent successfully! Tweet: '{Tweet}'.", tweet);
            return new PostTweetResponse(TweetSendStatus.Sent, TweetId: await ReadPostedTweetId(response));
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody = responseBody.ToLower();
            if (responseBody.Contains("duplicate content"))
            {
                logger.LogError(
                    "Failed to send tweet. Duplicate-tweet. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Body: '{ReadAsStringAsync}', Tweet: '{Tweet}'.", response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync(), tweet);
                return new PostTweetResponse(TweetSendStatus.DuplicateForbidden, tweetData.text);
            }

            if (responseBody.Contains("too many requests"))
            {
                logger.LogError(
                    "Failed to send tweet. Too-many-requests. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Body: '{ReadAsStringAsync}', Tweet: '{Tweet}'.", response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync(), tweet);
                return new PostTweetResponse(TweetSendStatus.TooManyRequests, tweetData.text);
            }
        }
        else if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfterHeaders = response.Headers.Where(x => x.Key.StartsWith("x-rate-limit"))
                .Select(x => new {name = x.Key, value = x.Value.FirstOrDefault()}).ToArray();

            var resetDetails = "";
            if (retryAfterHeaders.SingleOrDefault(x =>
                    x.name == "x-rate-limit-reset" && !string.IsNullOrWhiteSpace(x.value)) != null)
            {
                var rateLimitReset = long.Parse(retryAfterHeaders.Single(x => x.name == "x-rate-limit-reset").value!);
                var resetAt = DateTimeOffset.FromUnixTimeSeconds(rateLimitReset);
                resetDetails = $"Reset-At: '{resetAt:G}'. ";
            }

            logger.LogError(
                "Failed to send tweet. Too-many-requests. {ResetDetails}Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Headers: {Serialize} Body: '{ReadAsStringAsync}', Tweet: '{Tweet}'.", resetDetails, response.ReasonPhrase, response.StatusCode, JsonSerializer.Serialize(retryAfterHeaders), await response.Content.ReadAsStringAsync(), tweet);
            return new PostTweetResponse(TweetSendStatus.TooManyRequests, tweetData.text);
        }

        logger.LogError(
            "Failed to send tweet. Reason-Phrase: '{ResponseReasonPhrase}'. Status-code: '{ResponseStatusCode}'. Body: '{ReadAsStringAsync}', Tweet: '{Tweet}'.", response.ReasonPhrase, response.StatusCode, await response.Content.ReadAsStringAsync(), tweet);

        return new PostTweetResponse(TweetSendStatus.Failed, tweetData.text);
    }

    public async Task<GetTweetsResponseWrapper> GetTweets()
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var createTweetRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.twitter.com/2/users/{_options.TwitterId}/tweets?tweet.fields=entities");

        using var httpClient = new HttpClient(oauth);

        using var response = await httpClient.SendAsync(createTweetRequest);
        if (response.IsSuccessStatusCode)
        {
            var tweetsResponse = await response.Content.ReadFromJsonAsync<GetTweetsResponse>();
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

        return new GetTweetsResponseWrapper(GetTweetsState.Other);
    }

    public async Task<bool> DeleteTweet(Tweet tweet)
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var createTweetRequest =
            new HttpRequestMessage(HttpMethod.Delete, $"https://api.twitter.com/2/tweets/{tweet.Id}");

        using var httpClient = new HttpClient(oauth);

        using var response = await httpClient.SendAsync(createTweetRequest);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation("Deleted tweet with id '{TweetId}'.", tweet.Id);
            return true;
        }

        logger.LogError("Failed to delete tweet with id '{TweetId}'. Status-code: '{ResponseStatusCode}'.", tweet.Id, response.StatusCode);
        return false;
    }

    public async Task<GetTweetsResponseWrapper> GetTimelineSince(DateTimeOffset startTime, string? paginationToken)
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var query = new StringBuilder(
            $"https://api.twitter.com/2/users/{_options.TwitterId}/tweets?tweet.fields=entities&max_results={TimelinePageSize}");
        query.Append("&start_time=").Append(Uri.EscapeDataString(
            startTime.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(paginationToken))
        {
            query.Append("&pagination_token=").Append(Uri.EscapeDataString(paginationToken));
        }

        using var getTweetsRequest = new HttpRequestMessage(HttpMethod.Get, query.ToString());
        using var httpClient = new HttpClient(oauth);
        using var response = await httpClient.SendAsync(getTweetsRequest);
        if (response.IsSuccessStatusCode)
        {
            var tweetsResponse = await response.Content.ReadFromJsonAsync<GetTweetsResponse>();
            if (tweetsResponse == null)
            {
                logger.LogError("Twitter GetTimelineSince (since '{StartTime:O}') returned success but no body.", startTime);
                return new GetTweetsResponseWrapper(GetTweetsState.Other);
            }

            // An empty window has no "data"; that is a complete, empty page rather than an error.
            var nextToken = string.IsNullOrWhiteSpace(tweetsResponse.MetaData?.NextToken)
                ? null
                : tweetsResponse.MetaData.NextToken;
            return new GetTweetsResponseWrapper(GetTweetsState.Retrieved, tweetsResponse.Tweets ?? [], nextToken);
        }

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new GetTweetsResponseWrapper(GetTweetsState.TooManyRequests);
        }

        logger.LogError("Twitter GetTimelineSince (since '{StartTime:O}') failed. Status-code: '{ResponseStatusCode}'.",
            startTime, response.StatusCode);
        return new GetTweetsResponseWrapper(GetTweetsState.Other);
    }

    public async Task<DeleteTweetState> DeleteTweetById(string tweetId)
    {
        if (!TweetResponseReader.IsTweetId(tweetId))
        {
            logger.LogError("Not deleting tweet: '{TweetId}' is not an X post id.", tweetId);
            return DeleteTweetState.Failed;
        }

        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        using var deleteTweetRequest =
            new HttpRequestMessage(HttpMethod.Delete, $"https://api.twitter.com/2/tweets/{tweetId}");
        using var httpClient = new HttpClient(oauth);
        using var response = await httpClient.SendAsync(deleteTweetRequest);
        var state = TweetResponseReader.ReadDeleteState(response.StatusCode,
            await response.Content.ReadAsStringAsync());
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
