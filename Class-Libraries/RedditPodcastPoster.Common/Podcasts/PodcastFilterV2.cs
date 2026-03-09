using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
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
                // Convert to legacy for FilteredEpisode
                var legacyEpisode = ToLegacyEpisode(v2Episode);
                filteredEpisodes.Add(new FilteredEpisode(legacyEpisode, matchedTerms.ToArray()));
                
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

    private static Episode ToLegacyEpisode(Models.V2.Episode v2Episode)
    {
        return new Episode
        {
            Id = v2Episode.Id,
            Title = v2Episode.Title,
            Description = v2Episode.Description,
            Release = v2Episode.Release,
            Length = v2Episode.Length,
            Explicit = v2Episode.Explicit,
            Posted = v2Episode.Posted,
            Tweeted = v2Episode.Tweeted,
            BlueskyPosted = v2Episode.BlueskyPosted,
            Ignored = v2Episode.Ignored,
            Removed = v2Episode.Removed,
            SpotifyId = v2Episode.SpotifyId,
            AppleId = v2Episode.AppleId,
            YouTubeId = v2Episode.YouTubeId,
            Urls = v2Episode.Urls,
            Subjects = v2Episode.Subjects,
            SearchTerms = v2Episode.SearchTerms,
            Language = v2Episode.SearchLanguage,
            Images = v2Episode.Images,
            TwitterHandles = v2Episode.TwitterHandles,
            BlueskyHandles = v2Episode.BlueskyHandles
        };
    }
}
