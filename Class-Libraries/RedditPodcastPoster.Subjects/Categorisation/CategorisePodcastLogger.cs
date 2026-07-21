using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Categorisation;

/// <summary>
/// Emits a stable Warning-level podcast categorise line so App Insights can show
/// which episodes were categorised and the subject delta in one KQL-friendly message.
/// Warning is intentional: Information is heavily sampled in production.
/// </summary>
public static class CategorisePodcastLogger
{
    public const string MessagePrefix = "Categorise podcast:";

    public static void Log(
        ILogger logger,
        Guid podcastId,
        string podcastName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        logger.LogWarning("{Message}", FormatMessage(podcastId, podcastName, episodes));
    }

    /// <summary>
    /// Same content as the rendered Warning message (for unit tests / docs).
    /// </summary>
    public static string FormatMessage(
        Guid podcastId,
        string podcastName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        var episodeSummaries = episodes.Select(FormatEpisode);
        return
            $"{MessagePrefix} podcast-id='{podcastId}' podcast-name='{podcastName}' episodes=[{string.Join("; ", episodeSummaries)}]";
    }

    private static string FormatEpisode(CategoriseEpisodeDelta episode)
    {
        var delta = DescribeDelta(episode);
        return
            $"episode-id='{episode.EpisodeId}' title='{episode.Title}' {delta} persisted={episode.Persisted}";
    }

    private static string DescribeDelta(CategoriseEpisodeDelta episode)
    {
        if (episode.Added.Count == 0 && episode.Removed.Count == 0)
        {
            return $"unchanged before→after={FormatSubjects(episode.Before)}";
        }

        return
            $"before→after={FormatSubjects(episode.Before)}→{FormatSubjects(episode.After)} added={FormatSubjects(episode.Added)} removed={FormatSubjects(episode.Removed)}";
    }

    private static string FormatSubjects(IReadOnlyList<string> subjects) =>
        $"[{string.Join(", ", subjects.Select(s => $"'{s}'"))}]";
}
