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

    public async Task<bool> Send(string tweet)
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
            return true;
        }

        logger.LogError(
            $"Failed to send tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}'.");

        return false;
    }
}