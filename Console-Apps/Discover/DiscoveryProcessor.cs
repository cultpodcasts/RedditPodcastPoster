using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    IDiscoveryServiceConfigProvider discoveryConfigProvider,
    IDiscoveryService discoveryService,
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
            Console.WriteLine(new string('-', 40));
            if (episode.Url != null)
            {
                Console.WriteLine(episode.Url);
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(episode.EpisodeName);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(episode.ShowName);
            if (!string.IsNullOrWhiteSpace(episode.Description))
            {
                var description = episode.Description;
                var min = Math.Min(description.Length, 200);
                Console.ForegroundColor = fg;
                Console.WriteLine(description[..min]);
            }

            Console.ForegroundColor = ConsoleColor.DarkMagenta;
            Console.WriteLine(episode.Released.ToString("g"));
            if (episode.Length != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(episode.Length);
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            foreach (var subject in episode.Subjects)
            {
                Console.WriteLine(subject);
            }

            if (episode.Views.HasValue || episode.MemberCount.HasValue)
            {
                Console.ForegroundColor = ConsoleColor.DarkGreen;
                const string unknown = "Unknown";
                Console.WriteLine(
                    $"Views: {(episode.Views.HasValue ? episode.Views.Value : unknown)}, Members: {(episode.MemberCount.HasValue ? episode.MemberCount.Value : unknown)}");
            }

            Console.ForegroundColor = fg;
            Console.WriteLine();
        }

        return new DiscoveryResponse(discoveryBegan);
    }
}