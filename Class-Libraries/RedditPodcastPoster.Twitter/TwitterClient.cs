using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OAuth;
using RedditPodcastPoster.Twitter.Dtos;
using RedditPodcastPoster.Twitter.Models;

namespace RedditPodcastPoster.Twitter;

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
            logger.LogInformation($"Tweet sent successfully! Tweet: '{tweet}'.");
            return new PostTweetResponse(TweetSendStatus.Sent);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody = responseBody.ToLower();
            if (responseBody.Contains("duplicate content"))
            {
                logger.LogError(
                    $"Failed to send tweet. Duplicate-tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
                return new PostTweetResponse(TweetSendStatus.DuplicateForbidden, tweetData.text);
            }

            if (responseBody.Contains("too many requests"))
            {
                logger.LogError(
                    $"Failed to send tweet. Too-many-requests. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
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
                $"Failed to send tweet. Too-many-requests. {resetDetails}Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Headers: {JsonSerializer.Serialize(retryAfterHeaders)} Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
            return new PostTweetResponse(TweetSendStatus.TooManyRequests, tweetData.text);
        }

        logger.LogError(
            $"Failed to send tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");

        return new PostTweetResponse(TweetSendStatus.Failed, tweetData.text);
    }

    public async Task<GetTweetsResponseWrapper> GetTweets()
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var createTweetRequest = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.twitter.com/2/users/{_options.TwitterId}/tweets");

        using var httpClient = new HttpClient(oauth);

        using var response = await httpClient.SendAsync(createTweetRequest);
        if (response.IsSuccessStatusCode)
        {
            var tweetsResponse = await response.Content.ReadFromJsonAsync<GetTweetsResponse>();
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
            logger.LogInformation($"Deleted tweet with id '{tweet.Id}'.");
            return true;
        }

        logger.LogError($"Failed to delete tweet with id '{tweet.Id}'. Status-code: '{response.StatusCode}'.");
        return false;
    }
}