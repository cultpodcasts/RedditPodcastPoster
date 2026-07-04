using System.Globalization;
using System.Text.RegularExpressions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.Episodes.Matching;

public interface IEpisodePlatformMatcher
{
    bool IsMatch(
        Episode existingEpisode,
        Episode incomingEpisode,
        Regex? episodeMatchRegex,
        Podcast podcast,
        IReadOnlyList<Episode> existingEpisodes);
}
