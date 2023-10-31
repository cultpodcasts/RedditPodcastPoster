using CommandLine;

namespace YouTubePushNotificationSubcribe;

public class SubscribeRequest
{
    [Option('p', "podcast-id", Required = false,
        HelpText = "The Id of the podcast to enable push-notifications")]
    public Guid? PodcastId { get; set; }

    [Option('u', "unsubscribe-podcast-id", Required = false,
        HelpText = "The Id of the podcast to disable push-notifications")]
    public Guid? UnsubscribePodcastId { get; set; }

    [Option('a', "renew-all-leases", Required = false, Default = false,
        HelpText = "Renew All Leases")]
    public bool RenewAllLeases { get; set; }
}