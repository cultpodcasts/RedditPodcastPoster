using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OAuth;

namespace Indexer.Tweets;

public class TwitterClient : ITwitterClient
{
    private readonly ILogger<TwitterClient> _logger;
    private readonly TwitterOptions _options;

    public TwitterClient(IOptions<TwitterOptions> options, ILogger<TwitterClient> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> Send(string tweet)
    {
        var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret);

        var tweetData = new {text = tweet};
        var jsonData = JsonConvert.SerializeObject(tweetData);

        var createTweetRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.twitter.com/2/tweets")
        {
            Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
        };

        using var httpClient = new HttpClient(oauth);

        var response = await httpClient.SendAsync(createTweetRequest);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation($"Tweet sent successfully! Tweet: '{tweet}'.");
            return true;
        }

        _logger.LogError(
            $"Failed to send tweet. Reason-Phrase: '{response.ReasonPhrase}'. Status-code: '{response.StatusCode}'. Body: '{await response.Content.ReadAsStringAsync()}'.");
        return false;
    }
}