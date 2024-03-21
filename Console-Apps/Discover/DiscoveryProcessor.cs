using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Discovery;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Subjects;

namespace Discover;

public class DiscoveryProcessor(
    ISearchProvider searchProvider,
    IPodcastRepository podcastRepository,
    ISubjectMatcher subjectMatcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    private readonly IList<string> _ignoreTerms = new[]
    {
        "cult of the lamb".ToLower(),
        "cult of lamb".ToLower(),
        "COTL".ToLower(),
        "cult of the lab".ToLower(),
        "Cult of the Lamp".ToLower(),
        "Cult of the Lumb".ToLower(),
        "Blue Oyster Cult".ToLower(),
        "Blue Öyster Cult".ToLower(),
        "Living Colour".ToLower(),
        "She Sells Sanctuary".ToLower(),
        "Far Cry".ToLower()
    };

    public async Task Process(DiscoveryRequest request)
    {
        var fg = Console.ForegroundColor;
        DateTime since;
        if (request.Since.HasValue)
        {
            since = request.Since.Value;
        }
        else if (request.NumberOfHours.HasValue)
        {
            since = DateTime.UtcNow.Subtract(TimeSpan.FromHours(request.NumberOfHours.Value));
        }
        else
        {
            throw new InvalidOperationException("Unable to determine baseline-time to discover from.");
        }

        Console.WriteLine($"Discovering items released since '{since:O}'.");

        var indexingContext = new IndexingContext(
            since,
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
        var discoveryBegan = DateTime.UtcNow;
        Console.WriteLine($"Initiating discovery at '{discoveryBegan:O}'.");

        var results = await searchProvider.GetEpisodes(indexingContext, discoveryConfig);
        var podcastIds = podcastRepository.GetAllBy(podcast =>
                podcast.IndexAllEpisodes || podcast.EpisodeIncludeTitleRegex != string.Empty,
            x => new
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
                var subjects = await subjectMatcher.MatchSubjects(new Episode
                    {Title = episode.EpisodeName, Description = episode.Description});
                Console.WriteLine(new string('-', 40));
                var description = episode.Description;
                var min = Math.Min(description.Length, 200);
                if (episode.Url != null)
                {
                    Console.WriteLine(episode.Url);
                }

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(episode.EpisodeName);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(episode.ShowName);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    Console.ForegroundColor = fg;
                    Console.WriteLine(description[..min]);
                }

                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.WriteLine(episode.Released.ToString("g"));
                if (episode.Length != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkBlue;
                    Console.WriteLine(episode.Length);
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                foreach (var subjectMatch in subjects.OrderByDescending(x => x.MatchResults.Sum(y => y.Matches)))
                {
                    Console.WriteLine(subjectMatch.Subject.Name);
                }

                Console.ForegroundColor = fg;
                Console.WriteLine();
            }
        }

        Console.WriteLine($"Discovery initiated at '{discoveryBegan:O}'.");
    }
}