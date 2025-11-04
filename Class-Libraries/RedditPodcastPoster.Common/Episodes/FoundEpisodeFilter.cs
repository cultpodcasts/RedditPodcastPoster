using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class FoundEpisodeFilter(ILogger<FoundEpisodeFilter> logger) : IFoundEpisodeFilter
{
    public IList<Episode> ReduceEpisodes(Podcast podcast, IList<Episode> episodes)
    {
        var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var eliminatedEpisodes = episodes.Where(x => !includeEpisodeRegex.IsMatch(x.Title));
        if (eliminatedEpisodes.Any())
        {
            logger.LogInformation(
                "Eliminating {eliminatedEpisodesCount} episodes of podcast '{podcastName}' with id '{podcastId}' with titles [{titles}] as they do not match {nameofPodcastEpisodeIncludeTitleRegex} of value '{podcastEpisodeIncludeTitleRegex}'.",
                eliminatedEpisodes.Count(), 
                podcast.Name, 
                podcast.Id,
                string.Join(", ", eliminatedEpisodes.Select(x => $"'{x.Title}'")),
                nameof(podcast.EpisodeIncludeTitleRegex),
                podcast.EpisodeIncludeTitleRegex);
        }

        episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        return episodes;
    }
}