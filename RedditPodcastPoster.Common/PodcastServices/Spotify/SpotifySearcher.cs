﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
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
        var matches = podcasts!.Where(x => x.Name == podcastName);

        var matchingPodcasts = matches.Where(x => x.Name == podcastName);
        return matchingPodcasts;
    }

    public SimpleEpisode? FindMatchingEpisode(
        string episodeTitle,
        DateTime episodeRelease,
        IEnumerable<IEnumerable<SimpleEpisode>> episodeLists)
    {
        foreach (var episodeList in episodeLists)
        {
            var match = episodeList.SingleOrDefault(x => x.Name == episodeTitle);
            if (match == null)
            {
                var sameDateMatches = episodeList.Where(x =>
                    DateOnly.ParseExact(x.ReleaseDate, "yyyy-MM-dd") == DateOnly.FromDateTime(episodeRelease));
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