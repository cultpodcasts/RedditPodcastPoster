using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    ISearchProvider searchProvider,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Process(DiscoveryRequest request)
    {
        var indexingContext = new IndexingContext(DateTimeHelper.DaysAgo(request.NumberOfDays),
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var serviceConfigs = new List<DiscoveryConfig.ServiceConfig>
        {
//            new("Cult", DiscoveryService.YouTube),
            new("Cults", DiscoveryService.Spotify),
            new("Cult", DiscoveryService.Spotify),
            new("Scientology", DiscoveryService.Spotify),
            new("NXIVM", DiscoveryService.Spotify),
            new("FLDS", DiscoveryService.Spotify)
        };
        if (request.IncludeListenNotes)
        {
            serviceConfigs.Insert(0, new DiscoveryConfig.ServiceConfig("Cult", DiscoveryService.ListenNotes));
        }

        var discoveryConfig = new DiscoveryConfig(serviceConfigs);

        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);

        foreach (var episode in results)
        {
            var description = episode.Description;
            var min = Math.Min(description.Length, 200);
            if (episode.Url != null)
            {
                Console.WriteLine(episode.Url);
            }

            Console.WriteLine(episode.EpisodeName);
            Console.WriteLine(episode.ShowName);
            Console.WriteLine(description[..min]);
            Console.WriteLine(episode.Released.ToString("g"));
            Console.WriteLine();
        }
    }
}