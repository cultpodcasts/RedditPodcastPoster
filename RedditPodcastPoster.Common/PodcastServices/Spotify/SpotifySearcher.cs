using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifySearcher : ISpotifySearcher
{
    private readonly ILogger<SpotifySearcher> _logger;

    public SpotifySearcher(ILogger<SpotifySearcher> logger)
    {
        _logger = logger;
    }

    public IEnumerable<SimpleShow> FindMatchingPodcasts(string podcastName, List<SimpleShow>? podcasts)
    {
        var matches = podcasts!.Where(x => x.Name.ToLower().Trim() == podcastName.ToLower());

        return matches;
    }

    public SimpleEpisode? FindMatchingEpisode(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<IEnumerable<SimpleEpisode>> episodeLists)
    {
        foreach (var episodeList in episodeLists)
        {
            var match = episodeList.SingleOrDefault(x => x.Name.Trim() == episodeTitle.Trim());
            if (match == null && episodeRelease.HasValue)
            {
                var sameDateMatches =
                    episodeList.Where(x => x.ReleaseDate == "0000" ||
                                           DateOnly.ParseExact(x.ReleaseDate,
                                               "yyyy-MM-dd") ==
                                           DateOnly.FromDateTime(episodeRelease.Value));
                if (sameDateMatches.Count() > 1)
                {
                    return sameDateMatches.MaxBy(x => Levenshtein.CalculateSimilarity(episodeTitle, x.Name));
                }

                match = sameDateMatches.SingleOrDefault();
            }

            return match;
        }

        return null;
    }
}