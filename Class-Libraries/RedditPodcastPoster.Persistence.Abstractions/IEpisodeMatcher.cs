using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeMatcher
{
    bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex);
}