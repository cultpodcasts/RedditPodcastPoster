using System.Text;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public class EnrichmentResults(IList<EnrichmentResult> updatedEpisodes)
{
    public IList<EnrichmentResult> UpdatedEpisodes { get; init; } = updatedEpisodes;

    public override string ToString()
    {
        var report = new StringBuilder();
        report.AppendLine("Enriched Episodes:");
        foreach (var enrichmentResult in UpdatedEpisodes)
        {
            var youTubeReport = enrichmentResult.EnrichmentContext.YouTubeUrlUpdated
                ? $" YouTubeUrl: '{EpisodeServicePresence.TryGetUrl(enrichmentResult.Episode, ServiceKeys.YouTube)}'"
                : string.Empty;
            var spotifyReport = enrichmentResult.EnrichmentContext.SpotifyUrlUpdated
                ? $" SpotifyUrl: '{EpisodeServicePresence.TryGetUrl(enrichmentResult.Episode, ServiceKeys.Spotify)}'"
                : string.Empty;
            var appleReport = string.Empty;
            ;
            var episodeReport = string.Empty;
            if (enrichmentResult.EnrichmentContext.AppleUrlUpdated)
            {
                appleReport += $" AppleUrl: '{EpisodeServicePresence.TryGetUrl(enrichmentResult.Episode, ServiceKeys.Apple)}'";
            }

            if (enrichmentResult.EnrichmentContext.ReleaseUpdated)
            {
                episodeReport += $" ReleaseDate: {enrichmentResult.Episode.Release:R}";
            }

            if (enrichmentResult.EnrichmentContext.YouTubeIdUpdated)
            {
                youTubeReport += $" YouTube-Id: {EpisodeServicePresence.YouTubeEpisodeId(enrichmentResult.Episode)}";
            }

            report.AppendLine(
                $"Title: '{enrichmentResult.Episode.Title}'.{episodeReport}{youTubeReport}{appleReport}{spotifyReport} Episode-Id: '{enrichmentResult.Episode.Id}'.'");
        }

        return report.ToString();
    }
}