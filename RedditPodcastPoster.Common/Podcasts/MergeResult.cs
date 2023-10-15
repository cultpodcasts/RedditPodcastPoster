using System.Text;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class MergeResult
{
    public MergeResult(List<Episode> addedEpisodes,
        List<(Episode Existing, Episode NewDetails)> mergedEpisodes,
        List<IEnumerable<Episode>> failedEpisodes)
    {
        AddedEpisodes = addedEpisodes;
        MergedEpisodes = mergedEpisodes;
        FailedEpisodes = failedEpisodes;
    }

    public List<Episode> AddedEpisodes { get; init; }
    public List<(Episode Existing, Episode NewDetails)> MergedEpisodes { get; init; }
    public List<IEnumerable<Episode>> FailedEpisodes { get; init; }

    public string MergedEpisodesReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Merged Episodes:");
        foreach (var (episode, newEpisode) in MergedEpisodes)
        {
            report.AppendLine(
                $"Title: '{episode.Title} with Id '{episode.Id}' merged with SpotifyId '{newEpisode.SpotifyId}', AppleId '{newEpisode.AppleId}', YouTubeId '{newEpisode.YouTubeId}'.");
        }

        return report.ToString();
    }

    public string AddedEpisodesReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Added Episodes:");
        foreach (var episode in AddedEpisodes)
        {
            report.AppendLine(
                $"Title: '{episode.Title}', SpotifyId: '{episode.SpotifyId}', YouTubeId: '{episode.YouTubeId}', AppleId: '{episode.AppleId}', Episode-Id: '{episode.Id}'.");
        }

        return report.ToString();
    }

    public string FailedEpisodesReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Failed to ingest:");
        foreach (var episode in FailedEpisodes.SelectMany(x => x))
        {
            report.AppendLine(
                $"Title: '{episode.Title}', SpotifyId: '{episode.SpotifyId}', YouTubeId: '{episode.YouTubeId}', AppleId: '{episode.AppleId}'.");
        }

        return report.ToString();
    }
}