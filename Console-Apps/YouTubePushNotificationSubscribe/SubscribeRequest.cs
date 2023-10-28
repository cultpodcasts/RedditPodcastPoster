using CommandLine;

namespace YouTubePushNotificationSubcribe;

public class SubscribeRequest
{
    [Value(0, MetaName = "Podcast Id", HelpText = "The Id of the podcast to subscribe for YouTube push notifications")]
    public Guid PodcastId { get; set; }
}