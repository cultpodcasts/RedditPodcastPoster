using System.Diagnostics;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace EpisodeLanguageBackfill;

public class EpisodeLanguageBackfillProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<EpisodeLanguageBackfillProcessor> logger)
{
    private static int _lastProgressLength;
    private long _lastProgressWriteTicks;
    private const int ProgressIntervalMs = 150;

    public async Task<int> Run(EpisodeLanguageBackfillRequest request)
    {
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        var totalPodcasts = podcasts.Count;
        logger.LogInformation("Scanning {PodcastCount} podcasts.", totalPodcasts);

        var podcastsWithLanguage = podcasts
            .Where(podcast => !string.IsNullOrWhiteSpace(podcast.Language))
            .ToList();

        var totalEpisodesScanned = 0;
        var episodesToUpdate = 0;
        var episodesUpdated = 0;
        var episodesSkippedWithLanguage = 0;
        var episodesSkippedNoPodcastLanguage = 0;
        var languageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var podcastsCompleted = 0;
        var stopwatch = Stopwatch.StartNew();

        foreach (var podcast in podcasts)
        {
            var currentPodcast = podcastsCompleted + 1;
            var podcastLanguage = podcast.Language?.Trim();
            var podcastEpisodes = 0;

            await foreach (var episode in episodeRepository.GetByPodcastId(podcast.Id))
            {
                podcastEpisodes++;
                totalEpisodesScanned++;

                if (!string.IsNullOrWhiteSpace(episode.Language))
                {
                    episodesSkippedWithLanguage++;
                    MaybeWriteProgress(
                        stopwatch.Elapsed,
                        currentPodcast,
                        totalPodcasts,
                        totalEpisodesScanned,
                        request.Apply ? episodesUpdated : episodesToUpdate,
                        request.Apply);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(podcastLanguage))
                {
                    episodesSkippedNoPodcastLanguage++;
                    MaybeWriteProgress(
                        stopwatch.Elapsed,
                        currentPodcast,
                        totalPodcasts,
                        totalEpisodesScanned,
                        request.Apply ? episodesUpdated : episodesToUpdate,
                        request.Apply);
                    continue;
                }

                episodesToUpdate++;
                languageCounts.TryGetValue(podcastLanguage, out var count);
                languageCounts[podcastLanguage] = count + 1;

                if (request.Apply)
                {
                    episode.Language = podcastLanguage;
                    episode.SetPodcastProperties(podcast);
                    await episodeRepository.Save(episode);
                    episodesUpdated++;
                    MaybeWriteProgress(
                        stopwatch.Elapsed,
                        currentPodcast,
                        totalPodcasts,
                        totalEpisodesScanned,
                        episodesUpdated,
                        apply: true,
                        force: true);
                }
                else
                {
                    MaybeWriteProgress(
                        stopwatch.Elapsed,
                        currentPodcast,
                        totalPodcasts,
                        totalEpisodesScanned,
                        episodesToUpdate,
                        apply: false);
                }
            }

            podcastsCompleted++;
            WriteProgress(
                stopwatch.Elapsed,
                podcastsCompleted,
                totalPodcasts,
                totalEpisodesScanned,
                request.Apply ? episodesUpdated : episodesToUpdate,
                request.Apply,
                force: true);
        }

        ClearProgressLine();

        logger.LogInformation("Dry-run mode: {DryRun}", !request.Apply);
        logger.LogInformation("Podcasts scanned: {PodcastCount}", podcasts.Count);
        logger.LogInformation("Podcasts with default language set: {PodcastCount}", podcastsWithLanguage.Count);
        logger.LogInformation("Episodes scanned: {EpisodeCount}", totalEpisodesScanned);
        logger.LogInformation("Episodes already with language (left unchanged): {EpisodeCount}", episodesSkippedWithLanguage);
        logger.LogInformation("Episodes with no language and podcast has no language (left unchanged): {EpisodeCount}",
            episodesSkippedNoPodcastLanguage);
        logger.LogInformation("Episodes that would receive podcast default language: {EpisodeCount}", episodesToUpdate);

        foreach (var (language, count) in languageCounts.OrderByDescending(x => x.Value))
        {
            logger.LogInformation("  {Language}: {EpisodeCount}", language, count);
        }

        if (request.Apply)
        {
            logger.LogInformation("Episodes updated: {EpisodeCount}", episodesUpdated);
        }
        else if (episodesToUpdate > 0)
        {
            logger.LogInformation("Run with --apply to update {EpisodeCount} episodes.", episodesToUpdate);
        }

        return episodesToUpdate;
    }

    private void MaybeWriteProgress(
        TimeSpan elapsed,
        int currentPodcast,
        int totalPodcasts,
        int episodesScanned,
        int episodeCount,
        bool apply,
        bool force = false)
    {
        WriteProgress(elapsed, currentPodcast, totalPodcasts, episodesScanned, episodeCount, apply, force);
    }

    private void WriteProgress(
        TimeSpan elapsed,
        int currentPodcast,
        int totalPodcasts,
        int episodesScanned,
        int episodeCount,
        bool apply,
        bool force = false)
    {
        var now = Environment.TickCount64;
        if (!force && now - _lastProgressWriteTicks < ProgressIntervalMs)
        {
            return;
        }

        _lastProgressWriteTicks = now;

        var podcastsStarted = Math.Max(currentPodcast, 1);
        var estimatedTotalEpisodes = podcastsStarted > 0
            ? (long)Math.Round(episodesScanned * (double)totalPodcasts / podcastsStarted)
            : episodesScanned;
        estimatedTotalEpisodes = Math.Max(estimatedTotalEpisodes, episodesScanned);

        var percent = estimatedTotalEpisodes > 0
            ? Math.Min(99.9, 100d * episodesScanned / estimatedTotalEpisodes)
            : 100d * currentPodcast / totalPodcasts;

        var episodesPerSecond = episodesScanned / Math.Max(elapsed.TotalSeconds, 0.001);
        var remainingEpisodes = Math.Max(0, estimatedTotalEpisodes - episodesScanned);
        var eta = episodesPerSecond > 0
            ? TimeSpan.FromSeconds(remainingEpisodes / episodesPerSecond)
            : TimeSpan.Zero;

        var episodeLabel = apply ? "updated" : "to update";
        var message =
            $"  Podcast {currentPodcast:N0}/{totalPodcasts:N0} | episodes {episodesScanned:N0}/{estimatedTotalEpisodes:N0} ({percent:F1}%) | {episodesPerSecond:F1} ep/s | elapsed {FormatDuration(elapsed)} | ETA {FormatDuration(eta)} | {episodeLabel}: {episodeCount:N0}";
        var padding = Math.Max(0, _lastProgressLength - message.Length);
        Console.Write($"\r{message}{new string(' ', padding)}");
        _lastProgressLength = message.Length;
        Console.Out.Flush();
    }

    private static void ClearProgressLine()
    {
        if (_lastProgressLength <= 0)
        {
            return;
        }

        Console.Write($"\r{new string(' ', _lastProgressLength)}\r");
        _lastProgressLength = 0;
        Console.Out.Flush();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";
        }

        if (duration.TotalMinutes >= 1)
        {
            return $"{(int)duration.TotalMinutes}m {duration.Seconds:D2}s";
        }

        return $"{duration.Seconds}s";
    }
}
