using System.Text;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Abstractions;

public record IndexPodcastResult(
    Podcast Podcast,
    MergeResult MergeResult,
    FilterResult FilterResult,
    EnrichmentResults EnrichmentResult,
    bool SpotifyBypassed,
    bool YouTubeBypassed)
{
    public bool Success => !SpotifyBypassed && !YouTubeBypassed && !MergeResult.FailedEpisodes.Any();

    public override string ToString()
    {
        var report = new StringBuilder();

        if (SpotifyBypassed || YouTubeBypassed || FilterResult.FilteredEpisodes.Any() ||
            MergeResult.FailedEpisodes.Any() || MergeResult.MergedEpisodes.Any() || MergeResult.AddedEpisodes.Any() ||
            EnrichmentResult.UpdatedEpisodes.Any())
        {
            report.AppendLine($"Podcast: '{Podcast.Name}', PodcastId: '{Podcast.Id}'.");

            if (SpotifyBypassed)
            {
                report.AppendLine("Spotify API reported an error during operation");
            }

            if (YouTubeBypassed)
            {
                report.AppendLine("YouTube API reported an error during operation");
            }

            if (FilterResult.FilteredEpisodes.Any())
            {
                report.Append(FilterResult);
            }

            if (MergeResult.FailedEpisodes.Any())
            {
                report.Append(MergeResult.FailedEpisodesReport());
            }

            if (MergeResult.AddedEpisodes.Any())
            {
                report.Append(MergeResult.AddedEpisodesReport());
            }

            if (MergeResult.MergedEpisodes.Any())
            {
                report.Append(MergeResult.MergedEpisodesReport());
            }

            if (EnrichmentResult.UpdatedEpisodes.Any())
            {
                report.Append(EnrichmentResult);
            }
        }

        return report.ToString();
    }
}