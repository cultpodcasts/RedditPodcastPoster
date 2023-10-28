using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.YouTubePushNotifications.Configuration;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PodcastYouTubePushNotificationSubscriber : IPodcastYouTubePushNotificationSubscriber
{
    private static readonly Uri NotificationSubscribeEndpoint =
        new("https://pubsubhubbub.appspot.com/subscribe", UriKind.Absolute);

    private readonly HttpClient _httpClient;
    private readonly ILogger<PodcastYouTubePushNotificationSubscriber> _logger;
    private readonly YouTubePushNotificationCallbackSettings _pushNotificationCallbackSettings;

    public PodcastYouTubePushNotificationSubscriber(
        HttpClient httpClient,
        IOptions<YouTubePushNotificationCallbackSettings> pushNotificationCallbackSettings,
        ILogger<PodcastYouTubePushNotificationSubscriber> logger
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _pushNotificationCallbackSettings = pushNotificationCallbackSettings.Value;
    }

    public async Task Renew(Podcast podcast)
    {
        var callbackUrl = new Uri(_pushNotificationCallbackSettings.CallbackBaseUrl, podcast.Id.ToString());
        var topicUrl = $"https://www.youtube.com/feeds/videos.xml?channel_id={podcast.YouTubeChannelId}";

        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("hub.callback", callbackUrl.ToString()),
            new KeyValuePair<string, string>("hub.topic", topicUrl),
            new KeyValuePair<string, string>("hub.verify", "async"),
            new KeyValuePair<string, string>("hub.mode", "subscribe"),
            new KeyValuePair<string, string>("hub.verify_token", string.Empty),
            new KeyValuePair<string, string>("hub.secret", string.Empty),
            new KeyValuePair<string, string>("hub.lease_numbers", string.Empty)
        });

        var result = await _httpClient.PostAsync(NotificationSubscribeEndpoint, formContent);
        if (!result.IsSuccessStatusCode)
        {
            var body = await result.Content.ReadAsStringAsync();
            _logger.LogError(
                $"Failure to subscribe podcast for youtube push notifications. Callback-url: '{callbackUrl}', topic-url:'{topicUrl}'. Result status-code: '{result.StatusCode.ToString()}', response-body: '{body}'.");
        }
    }
}