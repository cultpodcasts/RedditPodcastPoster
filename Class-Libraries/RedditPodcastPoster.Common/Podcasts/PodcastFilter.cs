﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastFilter(ILogger<PodcastFilter> logger) : IPodcastFilter
{
    public FilterResult Filter(Podcast podcast, List<string> eliminationTerms)
    {
        var filteredEpisodes = new List<FilteredEpisode>();
        var episodesToRemove = new List<Episode>();
        foreach (var podcastEpisode in podcast.Episodes.Where(x => !x.Removed))
        {
            var remove = false;
            var titleLower = podcastEpisode.Title.ToLower();
            var descriptionLower = podcastEpisode.Description.ToLower();
            var matchedTerms = new List<string>();
            foreach (var eliminationTerm in eliminationTerms)
            {
                remove = titleLower.Contains(eliminationTerm) || descriptionLower.Contains(eliminationTerm);
                if (remove)
                {
                    matchedTerms.Add(eliminationTerm);
                    logger.LogInformation(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to match with '{eliminationTerm}'.",
                        podcastEpisode.Title, podcast.Name, eliminationTerm);
                }
            }

            if (remove)
            {
                filteredEpisodes.Add(new FilteredEpisode(podcastEpisode, matchedTerms.ToArray()));
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