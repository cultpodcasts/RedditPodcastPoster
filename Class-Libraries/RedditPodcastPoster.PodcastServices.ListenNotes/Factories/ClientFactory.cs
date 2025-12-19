using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PodcastAPI;
using RedditPodcastPoster.PodcastServices.ListenNotes.Configuration;

namespace RedditPodcastPoster.PodcastServices.ListenNotes.Factories;

public class ClientFactory(
    IOptions<ListenNotesOptions> options,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<ClientFactory> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IClientFactory
{
    private readonly ListenNotesOptions _options = options.Value;

    public Client Create()
    {
        if (string.IsNullOrWhiteSpace(_options.Key))
        {
            logger.LogError($"{nameof(Create)}: Listen-notes key is missing");
            throw new ArgumentNullException($"{nameof(ListenNotesOptions)}.{nameof(ListenNotesOptions.Key)}");
        }

        logger.LogInformation(
            "{CreateName}: Using listen-notes application-key ending '{Substring}'.", nameof(Create), _options.Key.Substring(_options.Key.Length - 2));
        return new Client(_options.Key);
    }
}