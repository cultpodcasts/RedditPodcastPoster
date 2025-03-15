using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Finders;

public class SearchResultFinder(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<SearchResultFinder> logger
#pragma warning restore CS9113 // Parameter is unread.
) : ISearchResultFinder
{
    private const int MinFuzzyScore = 65;
    private static readonly long TimeDifferenceThreshold = TimeSpan.FromSeconds(30).Ticks;
    private static readonly long BroaderTimeDifferenceThreshold = TimeSpan.FromSeconds(90).Ticks;

    public IEnumerable<SimpleShow> FindMatchingPodcasts(string podcastName, List<SimpleShow>? podcasts)
    {
        if (podcasts == null)
        {
            return [];
        }

        var matches = podcasts.Where(x => x.Name.ToLower().Trim() == podcastName.ToLower());
        return matches;
    }

    public SimpleEpisode? FindMatchingEpisodeByLength(
        string episodeTitle,
        TimeSpan episodeLength,
        IEnumerable<SimpleEpisode> episodes,
        Func<SimpleEpisode, bool>? reducer = null)
    {
        var requestEpisodeTitle = WebUtility.HtmlDecode(episodeTitle.Trim());

        var match = episodes.SingleOrDefault(x =>
        {
            var trimmedEpisodeTitle = WebUtility.HtmlDecode(x.Name.Trim());
            return trimmedEpisodeTitle == requestEpisodeTitle ||
                   trimmedEpisodeTitle.Contains(requestEpisodeTitle) ||
                   requestEpisodeTitle.Contains(trimmedEpisodeTitle);
        });
        if (match == null)
        {
            IEnumerable<SimpleEpisode> sampleList;
            if (reducer != null)
            {
                sampleList = episodes.Where(reducer);
            }
            else
            {
                sampleList = episodes;
            }

            var sameLength = sampleList
                .Where(x => Math.Abs((x.GetDuration() - episodeLength).Ticks) < TimeDifferenceThreshold);

            if (sameLength.Count() > 1)
            {
                return FuzzyMatcher.Match(episodeTitle, sameLength, x => x.Name);
            }

            match = sameLength.SingleOrDefault(x =>
                FuzzyMatcher.IsMatch(episodeTitle, x, y => y.Name, MinFuzzyScore));

            if (match == null)
            {
                sameLength = sampleList
                    .Where(x => Math.Abs((x.GetDuration() - episodeLength).Ticks) < BroaderTimeDifferenceThreshold);
                return FuzzyMatcher.Match(episodeTitle, sameLength, x => x.Name, MinFuzzyScore);
            }
        }

        return match;
    }

    public SimpleEpisode? FindMatchingEpisodeByDate(
        string episodeTitle,
        DateTime? episodeRelease,
        IEnumerable<SimpleEpisode> episodes)
    {
        var lowerTitle = episodeTitle.ToLowerInvariant();
        var matches = episodes.Where(x =>
        {
            var itemLowerTitle = x.Name.Trim().ToLowerInvariant();
            return itemLowerTitle == lowerTitle || itemLowerTitle.Contains(lowerTitle) ||
                   lowerTitle.Contains(itemLowerTitle);
        });
        var match = matches.FirstOrDefault();
        if (match == null && episodeRelease.HasValue)
        {
            var sameDateMatches =
                episodes.Where(x =>
                {
                    var episodeReleaseDateTime = x.GetReleaseDate();
                    if (episodeReleaseDateTime == DateTime.MinValue)
                    {
                        return true;
                    }

                    var episodeReleaseDate = DateOnly.FromDateTime(episodeReleaseDateTime);
                    var expectedDateOnly = DateOnly.FromDateTime(episodeRelease.Value);
                    var dateDiff = Math.Abs(expectedDateOnly.DayNumber - episodeReleaseDate.DayNumber);

                    return episodeReleaseDate == expectedDateOnly || dateDiff <= 1;
                });
            if (sameDateMatches.Count() > 1)
            {
                return FuzzyMatcher.Match(episodeTitle, sameDateMatches, x => x.Name, MinFuzzyScore);
            }

            match = sameDateMatches.SingleOrDefault(x =>
                FuzzyMatcher.IsMatch(episodeTitle, x, x => x.Name, MinFuzzyScore));
        }

        return match;
    }
}