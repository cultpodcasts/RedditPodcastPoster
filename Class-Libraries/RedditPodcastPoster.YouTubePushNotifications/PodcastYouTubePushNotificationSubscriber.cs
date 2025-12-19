using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PodcastYouTubePushNotificationSubscriber(
    HttpClient httpClient,
    IPodcastYouTubePushNotificationUrlAdaptor podcastYouTubePushNotificationUrlAdaptor,
    ILogger<PodcastYouTubePushNotificationSubscriber> logger)
    : IPodcastYouTubePushNotificationSubscriber
{
    private static readonly Uri NotificationSubscribeEndpoint =
        new("https://pubsubhubbub.appspot.com/subscribe", UriKind.Absolute);

    public async Task Renew(Podcast podcast)
    {
        await SendUpdate(podcast, Constants.ModeSubscribe);
    }

    public async Task Unsubscribe(Podcast podcast)
    {
        await SendUpdate(podcast, Constants.ModeUnsubscribe);
    }

    private async Task SendUpdate(Podcast podcast, string mode)
    {
        var (callbackUrl, topicUrl) = podcastYouTubePushNotificationUrlAdaptor.GetPodcastSubscriptionUrls(podcast);

        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("hub.callback", callbackUrl.ToString()),
            new KeyValuePair<string, string>("hub.topic", topicUrl.ToString()),
            new KeyValuePair<string, string>("hub.verify", "async"),
            new KeyValuePair<string, string>("hub.mode", mode),
            new KeyValuePair<string, string>("hub.verify_token", string.Empty),
            new KeyValuePair<string, string>("hub.secret", string.Empty),
            new KeyValuePair<string, string>("hub.lease_numbers", string.Empty)
        });

        var result = await httpClient.PostAsync(NotificationSubscribeEndpoint, formContent);
        if (!result.IsSuccessStatusCode)
        {
            var body = await result.Content.ReadAsStringAsync();
            logger.LogError(
                "Failure to subscribe podcast for youtube push notifications. Callback-url: '{CallbackUrl}', topic-url:'{TopicUrl}'. Result status-code: '{ToString}', response-body: '{Body}'.", callbackUrl, topicUrl, result.StatusCode.ToString(), body);
        }
    }
}