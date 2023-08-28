using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifySearcher : ISpotifySearcher
{
    private readonly ILogger<SpotifySearcher> _logger;

    public SpotifySearcher(ILogger<SpotifySearcher> logger)
    {
        _logger = logger;
    }

    public IEnumerable<SimpleShow> FindMatchingPodcasts(Podcast podcast, List<SimpleShow>? podcasts)
    {
        var matches = podcasts!.Where(x => x.Name == podcast.Name);

        var matchingPodcasts = matches.Where(x => x.Name == podcast.Name);
        return matchingPodcasts;
    }

    public SimpleEpisode? FindMatchingEpisode(Episode episode,
        IEnumerable<IEnumerable<SimpleEpisode>> episodeLists)
    {
        foreach (var episodeList in episodeLists)
        {
            var match = episodeList.SingleOrDefault(x => x.Name == episode.Title);
            if (match == null)
            {
                var sameDateMatches = episodeList.Where(x =>
                    DateOnly.ParseExact(x.ReleaseDate, "yyyy-MM-dd") == DateOnly.FromDateTime(episode.Release));
                if (sameDateMatches.Count() > 1)
                {
                    return sameDateMatches.MaxBy(x => Levenshtein.CalculateSimilarity(episode.Title, x.Name));
                }

                match = sameDateMatches.SingleOrDefault();
            }

            return match;
        }

        return null;
    }
}