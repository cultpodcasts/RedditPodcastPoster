using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Matching;

public interface IEpisodeMatcher
{
    bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex);
}