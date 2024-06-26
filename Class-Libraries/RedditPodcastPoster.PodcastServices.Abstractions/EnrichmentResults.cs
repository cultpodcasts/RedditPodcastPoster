﻿using System.Text;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

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
                ? $" YouTubeUrl: '{enrichmentResult.Episode.Urls.YouTube}'"
                : string.Empty;
            var spotifyReport = enrichmentResult.EnrichmentContext.SpotifyUrlUpdated
                ? $" SpotifyUrl: '{enrichmentResult.Episode.Urls.Spotify}'"
                : string.Empty;
            var appleReport = string.Empty;
            ;
            var episodeReport = string.Empty;
            if (enrichmentResult.EnrichmentContext.AppleUrlUpdated)
            {
                appleReport += $" AppleUrl: '{enrichmentResult.Episode.Urls.Apple}'";
            }

            if (enrichmentResult.EnrichmentContext.ReleaseUpdated)
            {
                episodeReport += $" ReleaseDate: {enrichmentResult.Episode.Release:R}";
            }

            if (enrichmentResult.EnrichmentContext.YouTubeIdUpdated)
            {
                youTubeReport += $" YouTube-Id: {enrichmentResult.Episode.YouTubeId}";
            }

            report.AppendLine(
                $"Title: '{enrichmentResult.Episode.Title}'.{episodeReport}{youTubeReport}{appleReport}{spotifyReport} Episode-Id: '{enrichmentResult.Episode.Id}'.'");
        }

        return report.ToString();
    }
}