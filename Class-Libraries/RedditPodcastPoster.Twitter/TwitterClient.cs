using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OAuth;

namespace RedditPodcastPoster.Twitter;

public class TwitterClient(IOptions<TwitterOptions> options, ILogger<TwitterClient> logger)
    : ITwitterClient
{
    private readonly TwitterOptions _options = options.Value;

    public async Task<TweetSendStatus> Send(string tweet)
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
            return TweetSendStatus.Sent;
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody = responseBody.ToLower();
            if (responseBody.Contains("duplicate content"))
            {
                logger.LogError(
                    $"Failed to send tweet. Duplicate-tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
                return TweetSendStatus.DuplicateForbidden;
            }

            if (responseBody.Contains("too many requests"))
            {
                logger.LogError(
                    $"Failed to send tweet. Too-many-requests. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
                return TweetSendStatus.TooManyRequests;
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
                resetDetails += $"Reset-At: '{resetAt:G}'. ";
            }

            if (retryAfterHeaders.SingleOrDefault(x =>
                    x.name == "x-rate-limit-remaining" && !string.IsNullOrWhiteSpace(x.value)) != null)
            {
                var rateLimitReset =
                    long.Parse(retryAfterHeaders.Single(x => x.name == "x-rate-limit-remaining").value!);
                var resetIn = TimeSpan.FromMilliseconds(rateLimitReset);
                resetDetails += $"Reset-In: '{resetIn:g}'. ";
            }

            logger.LogError(
                $"Failed to send tweet. Too-many-requests. {resetDetails}Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Headers: {JsonSerializer.Serialize(retryAfterHeaders)} Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");
            return TweetSendStatus.TooManyRequests;
        }

        logger.LogError(
            $"Failed to send tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}', Tweet: '{tweet}'.");

        return TweetSendStatus.Failed;
    }
}