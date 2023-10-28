using CommandLine;

namespace YouTubePushNotificationSubcribe;

public class SubscribeRequest
{
    [Option('p', "podcastid", Required = false, HelpText = "The Id of the podcast to enable push-notifications")]
    public Guid? PodcastId { get; set; }
}