using System.Text.RegularExpressions;
using RedditPodcastPoster.Models.V2;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeMatcher
{
    bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex);
    bool IsMatch(Models.Episode existingEpisode, Models.Episode episodeToMerge, Regex? episodeMatchRegex);
}