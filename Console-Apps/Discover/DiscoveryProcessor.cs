using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    IDiscoveryResultConsoleLogger discoveryResultConsoleLogger,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task<DiscoveryResponse> Process(DiscoveryRequest request)
    {
        var fg = Console.ForegroundColor;

        DateTime since;
        if (request.Since.HasValue)
        {
            if (request.Since.Value.ToUniversalTime() > DateTime.UtcNow)
            {
                throw new InvalidOperationException($"'{nameof(request)}.{nameof(request.Since)}' is in the future. ");
            }

            since = request.Since.Value.ToUniversalTime();
        }
        else if (request.NumberOfHours.HasValue)
        {
            since = DateTime.UtcNow.Subtract(TimeSpan.FromHours(request.NumberOfHours.Value));
        }
        else
        {
            throw new InvalidOperationException("Unable to determine baseline-time to discover from.");
        }

        Console.WriteLine(
            $"Discovering items released since '{since.ToUniversalTime():O}' (local:'{since.ToLocalTime():O}').");

        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var serviceConfigs = discoveryConfigProvider.GetServiceConfigs(request.ExcludeSpotify,
            request.IncludeYouTube, request.IncludeListenNotes);

        var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
        Console.WriteLine(
            $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}').");
        var discoveryConfig = new DiscoveryConfig(serviceConfigs, request.ExcludeSpotify);
        var discoveryResults = await discoveryService.GetDiscoveryResults(indexingContext, discoveryConfig);

        foreach (var episode in discoveryResults)
        {
            discoveryResultConsoleLogger.DisplayEpisode(episode, fg);
        }

        return new DiscoveryResponse(discoveryBegan);
    }
}