using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Episodes;

public class FoundEpisodeFilter : IFoundEpisodeFilter
{
    private readonly ILogger<FoundEpisodeFilter> _logger;

    public FoundEpisodeFilter(ILogger<FoundEpisodeFilter> logger)
    {
        _logger = logger;
    }

    public IList<Episode> ReduceEpisodes(Podcast podcast, IList<Episode> episodes)
    {
        var includeEpisodeRegex = new Regex(podcast.EpisodeIncludeTitleRegex,
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var eliminatedEpisodes = episodes.Where(x => !includeEpisodeRegex.IsMatch(x.Title));
        if (eliminatedEpisodes.Any())
        {
            _logger.LogInformation(
                $"Eliminating episodes of podcast '{podcast.Name}' with id '{podcast.Id}' with titles '{string.Join(", ", eliminatedEpisodes.Select(x => x.Title))}' as they do not match {nameof(podcast.EpisodeIncludeTitleRegex)} of value '{podcast.EpisodeIncludeTitleRegex}'.");
        }

        episodes = episodes.Where(x => includeEpisodeRegex.IsMatch(x.Title)).ToList();
        return episodes;
    }
}