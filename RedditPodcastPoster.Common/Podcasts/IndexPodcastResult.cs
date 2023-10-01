using System.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public record IndexPodcastResult(
    Podcast Podcast,
    MergeResult MergeResult,
    FilterResult FilterResult,
    bool SpotifyBypassed,
    bool YouTubeBypassed)
{
    public bool Success => !SpotifyBypassed && !YouTubeBypassed && !MergeResult.FailedEpisodes.Any();

    public override string ToString()
    {
        var report = new StringBuilder();

        if (SpotifyBypassed || YouTubeBypassed || FilterResult.FilteredEpisodes.Any() ||
            MergeResult.FailedEpisodes.Any() || MergeResult.MergedEpisodes.Any() || MergeResult.AddedEpisodes.Any())
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
                report.AppendLine("Removed due to terms:");
                foreach (var (episode, terms) in FilterResult.FilteredEpisodes)
                {
                    report.AppendLine(
                        $"Title: '{episode.Title}' with Id: {episode.Id}' for terms '{string.Join(", ", terms)}'.");
                }
            }

            if (MergeResult.FailedEpisodes.Any())
            {
                report.AppendLine("Failed to ingest:");
                foreach (var episode in MergeResult.FailedEpisodes.SelectMany(x => x))
                {
                    report.AppendLine(
                        $"Title: '{episode.Title}', SpotifyId: '{episode.SpotifyId}', YouTubeId: '{episode.SpotifyId}', AppleId: '{episode.SpotifyId}'.");
                }
            }

            if (MergeResult.AddedEpisodes.Any())
            {
                report.AppendLine("Added Episodes:");
                foreach (var episode in MergeResult.AddedEpisodes)
                {
                    report.AppendLine(
                        $"Title: '{episode.Title}', SpotifyId: '{episode.SpotifyId}', YouTubeId: '{episode.SpotifyId}', AppleId: '{episode.SpotifyId}'.");
                }
            }

            if (MergeResult.MergedEpisodes.Any())
            {
                report.AppendLine("Merged Episodes:");
                foreach (var (episode, newEpisode) in MergeResult.MergedEpisodes)
                {
                    report.AppendLine(
                        $"Title: '{episode.Title} with Id '{episode.Id}' merged with Spotify '{newEpisode.SpotifyId}', Apple '{newEpisode.AppleId}', YouTube '{newEpisode.YouTubeId}'.");
                }
            }
        }

        return report.ToString();
    }
}