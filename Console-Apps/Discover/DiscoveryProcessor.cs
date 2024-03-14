using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    ISearchProvider searchProvider,
    IPodcastRepository podcastRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private readonly IList<string> _ignoreTerms = new[]
    {
        "cult of the lamb".ToLower(), 
        "Blue Oyster Cult".ToLower()
    };

    public async Task Process(DiscoveryRequest request)
    {
        var indexingContext = new IndexingContext(
            DateTime.UtcNow.Subtract(TimeSpan.FromHours(request.NumberOfHours)),
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        var serviceConfigs = new List<DiscoveryConfig.ServiceConfig>();
        if (!request.ExcludeSpotify)
        {
            serviceConfigs.AddRange(new DiscoveryConfig.ServiceConfig[]
            {
                new("Cults", DiscoveryService.Spotify),
                new("Cult", DiscoveryService.Spotify),
                new("Scientology", DiscoveryService.Spotify),
                new("NXIVM", DiscoveryService.Spotify),
                new("FLDS", DiscoveryService.Spotify)
            });
        }

        if (request.IncludeYouTube)
        {
            serviceConfigs.AddRange(new DiscoveryConfig.ServiceConfig[]
            {
                new("Cult", DiscoveryService.YouTube)
            });
        }

        if (request.IncludeListenNotes)
        {
            serviceConfigs.Insert(0, new DiscoveryConfig.ServiceConfig("Cult", DiscoveryService.ListenNotes));
        }

        var discoveryConfig = new DiscoveryConfig(serviceConfigs, request.ExcludeSpotify);

        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);
        var podcastIds = podcastRepository.GetAllBy(podcast =>
            podcast.IndexAllEpisodes ||
            podcast.EpisodeIncludeTitleRegex != "", x => new
        {
            x.YouTubeChannelId,
            SpotifyShowId = x.SpotifyId
        });
        var indexedYouTubeChannelIds = await podcastIds.Select(x => x.YouTubeChannelId).Distinct().ToListAsync();
        var indexedSpotifyChannelIds = await podcastIds.Select(x => x.SpotifyShowId).Distinct().ToListAsync();

        foreach (var episode in results)
        {
            if ((episode.DiscoveryService == DiscoveryService.YouTube &&
                 indexedYouTubeChannelIds.Contains(episode.ServicePodcastId!)) ||
                (episode.DiscoveryService == DiscoveryService.Spotify &&
                 indexedSpotifyChannelIds.Contains(episode.ServicePodcastId!)))
            {
                continue;
            }

            var ignored = false;
            foreach (var ignoreTerm in _ignoreTerms)
            {
                if (episode.Description.ToLower().Contains(ignoreTerm) ||
                    episode.EpisodeName.ToLower().Contains(ignoreTerm))
                {
                    ignored = true;
                }
            }

            if (!ignored)
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
}