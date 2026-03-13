using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace EpisodeDriftDetector;

public class EpisodeDriftProcessor(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<EpisodeDriftProcessor> logger)
{
    public async Task Run(DriftDetectorRequest request)
    {
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        logger.LogInformation("Scanning {count} podcasts.", podcasts.Count);

        var totalDrifted = 0;
        var totalIdsMissing = 0;
        var totalCorrected = 0;

        foreach (var podcast in podcasts)
        {
            var episodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync();

            foreach (var episode in episodes)
            {
                var episodeSaved = false;
                var driftReasons = DetectMetadataDrift(podcast, episode);
                var missingIds = DetectMissingIds(episode);

                if (driftReasons.Count > 0)
                {
                    totalDrifted++;
                    logger.LogWarning(
                        "Drift on episode '{EpisodeId}' ({EpisodeTitle}) in podcast '{PodcastName}': {Reasons}",
                        episode.Id, episode.Title, podcast.Name, string.Join(", ", driftReasons));

                    if (request.Correct)
                    {
                        episode.SetPodcastProperties(podcast);
                        episodeSaved = true;
                    }
                }

                if (missingIds.Count > 0)
                {
                    totalIdsMissing++;
                    logger.LogWarning(
                        "Missing IDs on episode '{EpisodeId}' ({EpisodeTitle}) in podcast '{PodcastName}': {Ids}",
                        episode.Id, episode.Title, podcast.Name, string.Join(", ", missingIds));

                    if (request.Correct)
                    {
                        EnrichIds(episode);
                        episodeSaved = true;
                    }
                }

                if (episodeSaved)
                {
                    await episodeRepository.Save(episode);
                    totalCorrected++;
                }
            }
        }

        logger.LogInformation(
            "Scan complete. Drifted episodes: {drifted}. Episodes with missing IDs: {missing}. Corrections applied: {corrected}.",
            totalDrifted, totalIdsMissing, totalCorrected);

        if (!request.Correct && (totalDrifted > 0 || totalIdsMissing > 0))
        {
            logger.LogInformation("Run with --correct to apply fixes.");
        }
    }

    private static List<string> DetectMetadataDrift(Podcast podcast, Episode episode)
    {
        var reasons = new List<string>();

        if (episode.PodcastMetadataVersion != podcast.Timestamp)
        {
            reasons.Add(
                $"podcastMetadataVersion={episode.PodcastMetadataVersion} != podcast._ts={podcast.Timestamp}");
        }

        if (episode.PodcastName != podcast.Name?.Trim())
        {
            reasons.Add($"podcastName='{episode.PodcastName}' != '{podcast.Name?.Trim()}'");
        }

        if (episode.PodcastSearchTerms != podcast.SearchTerms?.Trim())
        {
            reasons.Add($"podcastSearchTerms mismatch");
        }

        if (episode.PodcastLanguage != podcast.Language?.Trim())
        {
            reasons.Add($"podcastLanguage='{episode.PodcastLanguage}' != '{podcast.Language?.Trim()}'");
        }

        if (episode.PodcastRemoved != podcast.Removed)
        {
            reasons.Add($"podcastRemoved={episode.PodcastRemoved} != podcast.Removed={podcast.Removed}");
        }

        return reasons;
    }

    private static List<string> DetectMissingIds(Episode episode)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(episode.SpotifyId) && episode.Urls.Spotify != null)
        {
            missing.Add("spotifyId (url present)");
        }

        if (string.IsNullOrWhiteSpace(episode.YouTubeId) && episode.Urls.YouTube != null)
        {
            missing.Add("youTubeId (url present)");
        }

        if (episode.AppleId == null && episode.Urls.Apple != null)
        {
            missing.Add("appleId (url present)");
        }

        return missing;
    }

    private static void EnrichIds(Episode episode)
    {
        if (string.IsNullOrWhiteSpace(episode.SpotifyId) && episode.Urls.Spotify != null)
        {
            var id = SpotifyIdResolver.GetEpisodeId(episode.Urls.Spotify);
            if (!string.IsNullOrWhiteSpace(id))
            {
                episode.SpotifyId = id;
            }
        }

        if (string.IsNullOrWhiteSpace(episode.YouTubeId) && episode.Urls.YouTube != null)
        {
            var id = YouTubeIdResolver.Extract(episode.Urls.YouTube);
            if (!string.IsNullOrWhiteSpace(id))
            {
                episode.YouTubeId = id;
            }
        }

        if (episode.AppleId == null && episode.Urls.Apple != null)
        {
            var id = AppleIdResolver.GetEpisodeId(episode.Urls.Apple);
            if (id != null)
            {
                episode.AppleId = id;
            }
        }
    }
}
