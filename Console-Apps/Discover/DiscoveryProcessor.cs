using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace Discover;

public class DiscoveryProcessor(
    ISpotifySearcher spotifySearcher,
    IListenNotesSearcher listenNotesSearcher,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<DiscoveryProcessor> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Process(DiscoveryRequest request)
    {
        var indexingContext = new IndexingContext(
            DateTimeHelper.DaysAgo(request.NumberOfDays),
            SkipSpotifyUrlResolving: false,
            SkipPodcastDiscovery: false,
            SkipExpensiveSpotifyQueries: false);

        IEnumerable<EpisodeResult> results = new EpisodeResult[] { };

        if (request.IncludeListenNotes)
        {
            results = results.Concat(await listenNotesSearcher.Search("\"Cult\"", indexingContext));
        }

        results = results.Concat(await spotifySearcher.Search("\"Cults\"", indexingContext));
        results = results.Concat(await spotifySearcher.Search("\"Cult\"", indexingContext));
        results = results.Concat(await spotifySearcher.Search("\"Scientology\"", indexingContext));
        results = results.Concat(await spotifySearcher.Search("\"NXIVM\"", indexingContext));
        results = results.Concat(await spotifySearcher.Search("\"FLDS\"", indexingContext));

        var individualResults = results
            .GroupBy(x => x.EpisodeName)
            .Select(x => x.FirstOrDefault(y => y.Url != null) ?? x.First())
            .OrderBy(x => x.Released);

        foreach (var episode in individualResults)
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