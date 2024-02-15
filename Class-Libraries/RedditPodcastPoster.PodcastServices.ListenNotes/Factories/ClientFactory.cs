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
        return new Client(_options.Key);
    }
}