using System.Text.RegularExpressions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public interface IEpisodeMatcher
{
    bool IsMatch(Episode existingEpisode, Episode episodeToMerge, Regex? episodeMatchRegex, Podcast podcast);
}
