using System.Text.RegularExpressions;

namespace RedditPodcastPoster.Persistence.Legacy;

public interface IEpisodeMatcher
{
    bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex);
}