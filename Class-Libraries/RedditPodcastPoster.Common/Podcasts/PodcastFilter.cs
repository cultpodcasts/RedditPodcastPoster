using Microsoft.Extensions.Logging;
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
                var removeForTerm = titleLower.Contains(eliminationTerm) || descriptionLower.Contains(eliminationTerm);
                if (removeForTerm)
                {
                    matchedTerms.Add(eliminationTerm);
                    logger.LogWarning(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to match with '{eliminationTerm}'.",
                        podcastEpisode.Title, podcast.Name, eliminationTerm);
                }

                remove |= removeForTerm;
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