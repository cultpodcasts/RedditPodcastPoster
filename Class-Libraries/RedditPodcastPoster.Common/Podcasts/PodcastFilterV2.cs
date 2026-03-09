using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.Common.Podcasts;

/// <summary>
/// V2 implementation that filters episodes from detached IEpisodeRepository based on elimination terms.
/// </summary>
public class PodcastFilterV2(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<PodcastFilterV2> logger) : IPodcastFilterV2
{
    public async Task<FilterResult> Filter(Guid podcastId, List<string> eliminationTerms)
    {
        var podcast = await podcastRepository.GetBy(x => x.Id == podcastId);
        if (podcast == null)
        {
            logger.LogWarning("Podcast with id '{PodcastId}' not found.", podcastId);
            return new FilterResult(new List<FilteredEpisode>());
        }

        // Load episodes from detached repository
        var v2Episodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();
        
        var filteredEpisodes = new List<FilteredEpisode>();
        var episodesToUpdate = new List<Models.V2.Episode>();

        foreach (var v2Episode in v2Episodes.Where(x => !x.Removed))
        {
            var remove = false;
            var titleLower = v2Episode.Title.ToLower();
            var descriptionLower = v2Episode.Description.ToLower();
            var matchedTerms = new List<string>();

            foreach (var eliminationTerm in eliminationTerms)
            {
                var removeForTerm = titleLower.Contains(eliminationTerm) || descriptionLower.Contains(eliminationTerm);
                if (removeForTerm)
                {
                    matchedTerms.Add(eliminationTerm);
                    logger.LogWarning(
                        "Removing episode '{episodeTitle}' of podcast '{podcastName}' due to match with '{eliminationTerm}'.",
                        v2Episode.Title, podcast.Name, eliminationTerm);
                }

                remove |= removeForTerm;
            }

            if (remove)
            {
                // Use V2 episode directly in filter results
                filteredEpisodes.Add(new FilteredEpisode(v2Episode, matchedTerms.ToArray()));
                
                // Mark as removed and add to update list
                v2Episode.Removed = true;
                episodesToUpdate.Add(v2Episode);
            }
        }

        // Save updated episodes
        if (episodesToUpdate.Any())
        {
            await episodeRepository.Save(episodesToUpdate);
        }

        return new FilterResult(filteredEpisodes);
    }
}
