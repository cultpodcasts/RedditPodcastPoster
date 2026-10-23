using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Subjects;

namespace RedditPodcastPoster.Subjects.Categorisation;

/// <summary>
/// Emits a stable Information-level podcast categorise line so App Insights can show
/// which episodes were categorised and the subject delta in one KQL-friendly message.
/// </summary>
public static class CategorisePodcastLogger
{
    public const string MessagePrefix = "Categorise podcast:";

    /// <summary>Stable prefix for TV episodes and news reports (App Insights keys off it).</summary>
    public const string CatalogueMessagePrefix = "Categorise catalogue:";

    public static void Log(
        ILogger logger,
        Guid podcastId,
        string podcastName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        logger.LogInformation("{Message}", FormatMessage(podcastId, podcastName, episodes));
    }

    public static void LogCatalogue(
        ILogger logger,
        Guid parentId,
        string parentName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        logger.LogInformation("{Message}", FormatCatalogueMessage(parentId, parentName, episodes));
    }

    /// <summary>
    /// Same content as the rendered log message (for unit tests / docs).
    /// </summary>
    public static string FormatMessage(
        Guid podcastId,
        string podcastName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        return Format(MessagePrefix, "podcast-id", podcastId, "podcast-name", podcastName, episodes);
    }

    /// <summary>
    /// Same content as the rendered catalogue log message (for unit tests / docs).
    /// </summary>
    public static string FormatCatalogueMessage(
        Guid parentId,
        string parentName,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        return Format(CatalogueMessagePrefix, "parent-id", parentId, "parent-name", parentName, episodes);
    }

    private static string Format(
        string prefix,
        string idLabel,
        Guid id,
        string nameLabel,
        string name,
        IReadOnlyList<CategoriseEpisodeDelta> episodes)
    {
        var episodeSummaries = episodes.Select(FormatEpisode);
        return
            $"{prefix} {idLabel}='{id}' {nameLabel}='{name}' episodes=[{string.Join("; ", episodeSummaries)}]";
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
