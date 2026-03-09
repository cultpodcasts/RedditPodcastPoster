using RedditPodcastPoster.Models.V2;
using System.Text;

namespace RedditPodcastPoster.Persistence.Abstractions;

public interface IEpisodeMerger
{
    EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge);
}

public class EpisodeMergeResult(
    IList<Episode> EpisodesToSave,
    IList<Episode> AddedEpisodes,
    IList<(Episode Existing, Episode NewDetails)> MergedEpisodes,
    IList<IEnumerable<Episode>> FailedEpisodes)
{
    public IList<Episode> EpisodesToSave { get; init; } = EpisodesToSave;
    public IList<Episode> AddedEpisodes { get; init; } = AddedEpisodes;
    public IList<(Episode Existing, Episode NewDetails)> MergedEpisodes { get; init; } = MergedEpisodes;
    public IList<IEnumerable<Episode>> FailedEpisodes { get; init; } = FailedEpisodes;

    public static EpisodeMergeResult Empty => new([], [], [], []);

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