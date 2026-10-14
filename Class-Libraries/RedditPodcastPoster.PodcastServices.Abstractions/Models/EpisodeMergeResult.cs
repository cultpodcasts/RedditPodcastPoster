using System.Text;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.PodcastServices.Abstractions.Models;

public class EpisodeMergeResult(
    IList<Episode> EpisodesToSave,
    IList<Episode> AddedEpisodes,
    IList<(Episode Existing, Episode NewDetails)> MergedEpisodes,
    IList<IEnumerable<Episode>> FailedEpisodes,
    IList<FailedEpisodeMerge>? FailedMerges = null)
{
    public IList<Episode> EpisodesToSave { get; init; } = EpisodesToSave;
    public IList<Episode> AddedEpisodes { get; init; } = AddedEpisodes;
    public IList<(Episode Existing, Episode NewDetails)> MergedEpisodes { get; init; } = MergedEpisodes;
    public IList<IEnumerable<Episode>> FailedEpisodes { get; init; } = FailedEpisodes;

    /// <summary>
    /// Per-incoming detail for <see cref="FailedEpisodes"/> (same order): the incoming episode, each candidate's
    /// tie-break score and the reason. Empty when the producer did not record detail.
    /// </summary>
    public IList<FailedEpisodeMerge> FailedMerges { get; init; } = FailedMerges ?? [];

    public static EpisodeMergeResult Empty => new([], [], [], []);

    public string MergedEpisodesReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Merged Episodes:");
        foreach (var (episode, newEpisode) in MergedEpisodes)
        {
            report.AppendLine(
                $"Title: '{episode.Title} with Id '{episode.Id}' merged with SpotifyId '{EpisodeServicePresence.SpotifyEpisodeId(newEpisode)}', AppleId '{EpisodeServicePresence.AppleEpisodeId(newEpisode)}', YouTubeId '{EpisodeServicePresence.YouTubeEpisodeId(newEpisode)}'.");
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
                $"Title: '{episode.Title}', SpotifyId: '{EpisodeServicePresence.SpotifyEpisodeId(episode)}', YouTubeId: '{EpisodeServicePresence.YouTubeEpisodeId(episode)}', AppleId: '{EpisodeServicePresence.AppleEpisodeId(episode)}', Episode-Id: '{episode.Id}'.");
        }

        return report.ToString();
    }

    public string FailedEpisodesReport()
    {
        var report = new StringBuilder();
        report.AppendLine("Failed to ingest:");
        if (FailedMerges.Count > 0)
        {
            foreach (var failed in FailedMerges)
            {
                report.AppendLine(
                    $"Incoming: '{failed.Incoming.Title}', SpotifyId: '{EpisodeServicePresence.SpotifyEpisodeId(failed.Incoming)}', YouTubeId: '{EpisodeServicePresence.YouTubeEpisodeId(failed.Incoming)}', AppleId: '{EpisodeServicePresence.AppleEpisodeId(failed.Incoming)}', Released: '{failed.Incoming.ReleaseUtc:O}', Length: '{failed.Incoming.Length}', Reason: '{failed.Reason}'.");
                foreach (var candidate in failed.Candidates)
                {
                    var score = candidate.Score?.ToString() ?? "n/a";
                    report.AppendLine(
                        $"  Candidate: '{candidate.Episode.Title}', Episode-Id: '{candidate.Episode.Id}', SpotifyId: '{EpisodeServicePresence.SpotifyEpisodeId(candidate.Episode)}', YouTubeId: '{EpisodeServicePresence.YouTubeEpisodeId(candidate.Episode)}', AppleId: '{EpisodeServicePresence.AppleEpisodeId(candidate.Episode)}', score: {score}.");
                }
            }

            return report.ToString();
        }

        foreach (var episode in FailedEpisodes.SelectMany(x => x))
        {
            report.AppendLine(
                $"Title: '{episode.Title}', SpotifyId: '{EpisodeServicePresence.SpotifyEpisodeId(episode)}', YouTubeId: '{EpisodeServicePresence.YouTubeEpisodeId(episode)}', AppleId: '{EpisodeServicePresence.AppleEpisodeId(episode)}'.");
        }

        return report.ToString();
    }
}

/// <summary>A failed merge of one incoming episode against several matched stored episodes.</summary>
public sealed record FailedEpisodeMerge(
    Episode Incoming,
    IReadOnlyList<FailedEpisodeMergeCandidate> Candidates,
    string Reason);

/// <param name="Score">Tie-break score, or null when the candidates were not scored.</param>
public sealed record FailedEpisodeMergeCandidate(Episode Episode, int? Score);
