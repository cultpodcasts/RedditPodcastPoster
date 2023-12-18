using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifySearcher : ISpotifySearcher
{
    private const int MinFuzzyScore = 70;
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long BroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;
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

    public SimpleEpisode? FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IList<IList<SimpleEpisode>> episodeLists,
        Func<SimpleEpisode, bool>? reducer = null)
    {
        foreach (var episodeList in episodeLists)
        {
            var match = episodeList.SingleOrDefault(x => x.Name.Trim() == episodeTitle.Trim());
            if (match == null)
            {
                IEnumerable<SimpleEpisode> sampleList;
                if (reducer != null)
                {
                    sampleList = episodeList.Where(reducer);
                }
                else
                {
                    sampleList = episodeList;
                }

                var sameLength = sampleList
                    .Where(x => Math.Abs((x.GetDuration() - episodeLength).Ticks) < TimeDifferenceThreshold);
                if (sameLength.Count() > 1)
                {
                    return FuzzyMatcher.Match(episodeTitle, sameLength, x => x.Name);
                }

                match = sameLength.SingleOrDefault();

                if (match == null)
                {
                    sameLength = sampleList
                        .Where(x => Math.Abs((x.GetDuration() - episodeLength).Ticks) < BroaderTimeDifferenceThreshold);
                    return FuzzyMatcher.Match(episodeTitle, sameLength, x => x.Name, MinFuzzyScore);
                }
            }

            return match;
        }

        return null;
    }

    public SimpleEpisode? FindMatchingEpisodeByDate(
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
                    episodeList.Where(x =>
                        x.ReleaseDate == "0000" || DateOnly.ParseExact(x.ReleaseDate, "yyyy-MM-dd") ==
                        DateOnly.FromDateTime(episodeRelease.Value));
                if (sameDateMatches.Count() > 1)
                {
                    return FuzzyMatcher.Match(episodeTitle, sameDateMatches, x => x.Name, MinFuzzyScore);
                }

                match = sameDateMatches.SingleOrDefault();
            }

            return match;
        }

        return null;
    }
}