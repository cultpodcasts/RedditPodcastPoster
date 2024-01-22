using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.YouTubePushNotifications.Configuration;

namespace RedditPodcastPoster.YouTubePushNotifications;

public class PodcastYouTubePushNotificationUrlAdaptor(
    IOptions<YouTubePushNotificationCallbackSettings> pushNotificationCallbackSettings,
    ILogger<PodcastYouTubePushNotificationUrlAdaptor> logger)
    : IPodcastYouTubePushNotificationUrlAdaptor
{
    private readonly ILogger<PodcastYouTubePushNotificationUrlAdaptor> _logger = logger;

    private readonly YouTubePushNotificationCallbackSettings _pushNotificationCallbackSettings =
        pushNotificationCallbackSettings.Value;

    public (Uri, Uri) GetPodcastSubscriptionUrls(Podcast podcast)
    {
        var callbackUrl = new Uri(_pushNotificationCallbackSettings.CallbackBaseUrl, podcast.Id.ToString());
        var topicUrl = new Uri($"https://www.youtube.com/feeds/videos.xml?channel_id={podcast.YouTubeChannelId}",
            UriKind.Absolute);
        return (callbackUrl, topicUrl);
    }
}