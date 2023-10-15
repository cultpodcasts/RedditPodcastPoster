using System.Text;

namespace RedditPodcastPoster.Common.PodcastServices;

public class EnrichmentResults
{
    public EnrichmentResults(IList<EnrichmentResult> updatedEpisodes)
    {
        UpdatedEpisodes = updatedEpisodes;
    }

    public IList<EnrichmentResult> UpdatedEpisodes { get; init; }

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
            var appleReport = "";
            if (enrichmentResult.EnrichmentContext.AppleUrlUpdated)
            {
                appleReport+= $" AppleUrl: '{enrichmentResult.Episode.Urls.Apple}'";
            }

            if (enrichmentResult.EnrichmentContext.AppleReleaseUpdated)
            {
                appleReport+= $" Apple-ReleaseDate: {enrichmentResult.Episode.Release:R}";
            }
            if (enrichmentResult.EnrichmentContext.YouTubeIdUpdated)
            {
                youTubeReport += $" YouTube-Id: {enrichmentResult.Episode.YouTubeId}";
            }

            report.AppendLine(
                $"Title: '{enrichmentResult.Episode.Title}'.{youTubeReport}{appleReport}{spotifyReport} Episode-Id: '{enrichmentResult.Episode.Id}'.'");
        }

        return report.ToString();
    }
}