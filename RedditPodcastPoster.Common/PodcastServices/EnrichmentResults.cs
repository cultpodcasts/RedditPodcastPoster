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
                appleReport = $" AppleUrl: '{enrichmentResult.Episode.Urls.Apple}'";
            }

            if (enrichmentResult.EnrichmentContext.AppleReleaseUpdated)
            {
                appleReport = $" Apple-ReleaseDate: {enrichmentResult.Episode.Release:R}";
            }

            report.AppendLine(
                $"Podcast '{enrichmentResult.Podcast.Name}' with id '{enrichmentResult.Podcast.Id}' episode '{enrichmentResult.Episode.Title} with Id '{enrichmentResult.Episode.Id}' updated.{youTubeReport}{appleReport}{spotifyReport}");
        }

        return report.ToString();
    }
}