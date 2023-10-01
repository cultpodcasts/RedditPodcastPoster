﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastFilter : IPodcastFilter
{
    private readonly ILogger<PodcastFilter> _logger;

    public PodcastFilter(ILogger<PodcastFilter> logger)
    {
        _logger = logger;
    }

    public FilterResult Filter(Podcast podcast, List<string> eliminationTerms)
    {
        IList<(Episode, string[])> filteredEpisodes = new List<(Episode, string[])>();
        var episodesToRemove = new List<Episode>();
        foreach (var podcastEpisode in podcast.Episodes.Where(x => !x.Removed))
        {
            var remove = false;
            var titleLower = podcastEpisode.Title.ToLower();
            var descriptionLower = podcastEpisode.Description.ToLower();
            var matchedTerms = new List<string>();
            foreach (var eliminationTerm in eliminationTerms)
            {
                remove |= titleLower.Contains(eliminationTerm) || descriptionLower.Contains(eliminationTerm);
                if (remove)
                {
                    matchedTerms.Add(eliminationTerm);
                    _logger.LogInformation(
                        $"Removing episode '{podcastEpisode.Title}' of podcast '{podcast.Name}' due to match with '{eliminationTerm}'.");
                }
            }

            if (remove)
            {
                filteredEpisodes.Add((podcastEpisode, matchedTerms.ToArray()));
                episodesToRemove.Add(podcastEpisode);
            }
        }

        foreach (var episodeToRemove in episodesToRemove)
        {
            episodeToRemove.Removed = true;
        }

        return new FilterResult(filteredEpisodes);
    }
}