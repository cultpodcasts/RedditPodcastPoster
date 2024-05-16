using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discovery;

[DurableTask(nameof(Discover))]
public class Discover(
    IOptions<DiscoverOptions> discoverOptions,
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
    ILogger<Discover> logger) : TaskActivity<DiscoveryContext, DiscoveryContext>
{
    private readonly DiscoverOptions _discoverOptions = discoverOptions.Value;

    public override async Task<DiscoveryContext> RunAsync(TaskActivityContext context, DiscoveryContext input)
    {
        var since = DateTime.UtcNow.Subtract(TimeSpan.Parse(_discoverOptions.Since));
        logger.LogInformation(
            $"Discovering items released since '{since.ToUniversalTime():O}' (local:'{since.ToLocalTime():O}').");

        var indexingContext = new IndexingContext(
            since,
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var serviceConfigs = discoveryConfigProvider.GetServiceConfigs(_discoverOptions.ExcludeSpotify,
            _discoverOptions.IncludeYouTube, _discoverOptions.IncludeListenNotes);

        var discoveryBegan = DateTime.UtcNow.ToUniversalTime();
        Console.WriteLine(
            $"Initiating discovery at '{discoveryBegan:O}' (local: '{discoveryBegan.ToLocalTime():O}').");
        var discoveryConfig = new DiscoveryConfig(serviceConfigs, _discoverOptions.ExcludeSpotify);

        var discoveryResults = await discoveryService.GetDiscoveryResults(indexingContext, discoveryConfig);


        //PERSIST DISCOVERY RESULTS

        return input with
        {
            DiscoveryBegan = discoveryBegan,
            Completed = DateTime.UtcNow.ToUniversalTime()
        };
    }
}