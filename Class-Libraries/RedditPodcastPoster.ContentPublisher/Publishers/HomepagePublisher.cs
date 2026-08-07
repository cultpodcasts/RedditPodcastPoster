using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.HomePage;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Providers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.ContentPublisher.Publishers;

public class HomepagePublisher(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITextSanitiser textSanitiser,
    ISubjectsProvider subjectsProvider,
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ILookupRepository lookupRepository,
    ILogger<HomepagePublisher> logger)
    : IHomepagePublisher
{
    // Flip to true to emit HomepagePublishTiming App Insights warnings + per-phase sanitise sums.
    private const bool EnableDiagnosticTiming = false;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;
    private readonly IReadOnlyList<Subject> _subjects = subjectsProvider.GetAll().ToBlockingEnumerable().ToList();

    public async Task<PublishHomepageResult> PublishHomepage()
    {
        if (!EnableDiagnosticTiming)
        {
            var (homepageContent, _) = await GetHomePage(CancellationToken.None);
            var published = await PublishHomepageToR2(homepageContent);
            return new PublishHomepageResult(published);
        }

        var total = Stopwatch.StartNew();
        var buildSw = Stopwatch.StartNew();
        var (timedContent, buildBreakdown) = await GetHomePage(CancellationToken.None);
        buildSw.Stop();

        var uploadSw = Stopwatch.StartNew();
        var homepagePublished = await PublishHomepageToR2(timedContent);
        uploadSw.Stop();
        total.Stop();

        // Stable App Insights / console search key: Message startswith "HomepagePublishTiming".
        // sanitise-*-sum-ms are CPU sums across parallel titles (can exceed wall sanitise-ms).
        logger.LogWarning(
            "HomepagePublishTiming total-ms='{TotalMs}' build-ms='{BuildMs}' upload-ms='{UploadMs}' recent-podcasts-ms='{RecentPodcastsMs}' recent-episodes-ms='{RecentEpisodesMs}' cache-ms='{CacheMs}' sanitise-ms='{SanitiseMs}' sanitise-prep-sum-ms='{SanitisePrepSumMs}' sanitise-rules-resolve-sum-ms='{SanitiseRulesResolveSumMs}' sanitise-lower-sum-ms='{SanitiseLowerSumMs}' sanitise-universal-kt-sum-ms='{SanitiseUniversalKtSumMs}' sanitise-lang-kt-sum-ms='{SanitiseLangKtSumMs}' sanitise-podcast-kt-sum-ms='{SanitisePodcastKtSumMs}' sanitise-subject-kt-sum-ms='{SanitiseSubjectKtSumMs}' sanitise-finish-sum-ms='{SanitiseFinishSumMs}' sanitise-desc-sum-ms='{SanitiseDescSumMs}' sanitise-title-max-ms='{SanitiseTitleMaxMs}' universal-kt-count='{UniversalKtCount}' en-kt-count='{EnKtCount}' en-lower-count='{EnLowerCount}' recent-episode-count='{RecentEpisodeCount}' recent-podcast-count='{RecentPodcastCount}' published='{Published}'.",
            total.ElapsedMilliseconds,
            buildSw.ElapsedMilliseconds,
            uploadSw.ElapsedMilliseconds,
            buildBreakdown.RecentPodcastsMs,
            buildBreakdown.RecentEpisodesMs,
            buildBreakdown.CacheMs,
            buildBreakdown.SanitiseMs,
            buildBreakdown.SanitisePrepSumMs,
            buildBreakdown.SanitiseRulesResolveSumMs,
            buildBreakdown.SanitiseLowerSumMs,
            buildBreakdown.SanitiseUniversalKtSumMs,
            buildBreakdown.SanitiseLangKtSumMs,
            buildBreakdown.SanitisePodcastKtSumMs,
            buildBreakdown.SanitiseSubjectKtSumMs,
            buildBreakdown.SanitiseFinishSumMs,
            buildBreakdown.SanitiseDescSumMs,
            buildBreakdown.SanitiseTitleMaxMs,
            buildBreakdown.UniversalKtCount,
            buildBreakdown.EnKtCount,
            buildBreakdown.EnLowerCount,
            buildBreakdown.RecentEpisodeCount,
            buildBreakdown.RecentPodcastCount,
            homepagePublished);

        return new PublishHomepageResult(homepagePublished);
    }

    private static bool IsRefreshWindow()
    {
        var utcNow = DateTime.UtcNow;
        return utcNow is { DayOfWeek: DayOfWeek.Monday, Hour: 0, Minute: < 20 };
    }

    private async Task<(HomePageModel Model, HomepageBuildTiming Breakdown)> GetHomePage(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var recentCutoff = DateTime.UtcNow.AddDays(-7);

        var recentPodcastsSw = Stopwatch.StartNew();
        var recentPodcasts = await podcastRepository
            .GetAllBy(
                x => (!x.Removed.IsDefined() || x.Removed == false) &&
                     x.LatestReleased.IsDefined() &&
                     x.LatestReleased != null &&
                     x.LatestReleased >= recentCutoff,
                x => new PodcastEntry
                {
                    Id = x.Id,
                    Name = x.Name,
                    TitleRegex = x.TitleRegex,
                    DescriptionRegex = x.DescriptionRegex,
                    KnownTerms = x.KnownTerms
                })
            .ToListAsync(ct);
        recentPodcastsSw.Stop();

        // Episode loads run in parallel with optional homepage-cache full scans inside ResolveHomePageCache.
        var recentEpisodesSw = Stopwatch.StartNew();
        var recentEpisodesTask = MeasureAsync(
            () => GetRecentEpisodes(recentPodcasts, recentCutoff, ct),
            recentEpisodesSw);

        var cacheSw = Stopwatch.StartNew();
        var homePageCache = await ResolveHomePageCache(recentEpisodesTask, ct);
        cacheSw.Stop();

        var recentEpisodes = await recentEpisodesTask;
        var activeEpisodeCount = homePageCache.ActiveEpisodeCount ?? 0;
        var podcasts = recentPodcasts.ToDictionary(x => x.Id);

        var orderedPodcasts = recentEpisodes
            .Select(episode =>
            {
                podcasts.TryGetValue(episode.PodcastId, out var podcast);
                return new PodcastResult
                {
                    PodcastName = episode.PodcastName ?? podcast?.Name ?? string.Empty,
                    TitleRegex = podcast?.TitleRegex ?? string.Empty,
                    DescriptionRegex = podcast?.DescriptionRegex ?? string.Empty,
                    EpisodeId = episode.EpisodeId,
                    EpisodeTitle = episode.EpisodeTitle,
                    EpisodeDescription = episode.EpisodeDescription,
                    Release = episode.Release,
                    Spotify = episode.Urls.Spotify,
                    Apple = episode.Urls.Apple,
                    YouTube = episode.Urls.YouTube,
                    BBC = episode.Urls.BBC,
                    InternetArchive = episode.Urls.InternetArchive,
                    Length = episode.Length,
                    Subjects = episode.Subjects.Count > 0 ? episode.Subjects.ToArray() : null,
                    Images = episode.Images,
                    KnownTerms = podcast?.KnownTerms,
                    Language = episode.Language
                };
            })
            .OrderByDescending(x => x.Release)
            .ToList();

        SanitiseTimingAggregator? sanitiseAgg = EnableDiagnosticTiming ? new SanitiseTimingAggregator() : null;
        var sanitiseSw = EnableDiagnosticTiming ? Stopwatch.StartNew() : null;
        var sanitizedPodcasts = await Task.WhenAll(orderedPodcasts.Select(p => Sanitise(p, sanitiseAgg)));
        sanitiseSw?.Stop();

        var model = new HomePageModel
        {
            EpisodeCount = activeEpisodeCount,
            RecentEpisodes = sanitizedPodcasts.Select(ToRecentEpisode),
            TotalDuration = homePageCache.TotalDuration
        };

        if (!EnableDiagnosticTiming || sanitiseAgg is null)
        {
            return (model, default);
        }

        var breakdown = new HomepageBuildTiming(
            recentPodcastsSw.ElapsedMilliseconds,
            recentEpisodesSw.ElapsedMilliseconds,
            cacheSw.ElapsedMilliseconds,
            sanitiseSw!.ElapsedMilliseconds,
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.PrepTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.RulesResolveTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.LowerCaseTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.UniversalKnownTermsTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.LanguageKnownTermsTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.PodcastKnownTermsTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.SubjectKnownTermsTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.FinishTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.DescriptionTicks),
            TitleSanitiseTiming.TicksToMs(sanitiseAgg.TitleMaxTicks),
            sanitiseAgg.UniversalKtCount,
            sanitiseAgg.EnKtCount,
            sanitiseAgg.EnLowerCount,
            recentEpisodes.Count,
            podcasts.Count);

        return (model, breakdown);
    }

    private static async Task<T> MeasureAsync<T>(Func<Task<T>> work, Stopwatch stopwatch)
    {
        stopwatch.Restart();
        try
        {
            return await work();
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<List<RecentEpisodeEntry>> GetRecentEpisodes(
        IReadOnlyList<PodcastEntry> podcasts,
        DateTime recentCutoff,
        CancellationToken ct)
    {
        var episodeLists = await Task.WhenAll(podcasts.Select(podcast => LoadRecentEpisodes(podcast, recentCutoff, ct)));
        return episodeLists.SelectMany(x => x).ToList();
    }

    private async Task<List<RecentEpisodeEntry>> LoadRecentEpisodes(
        PodcastEntry podcast,
        DateTime recentCutoff,
        CancellationToken ct)
    {
        var recentEpisodes = new List<RecentEpisodeEntry>();

        await foreach (var episode in episodeRepository.GetByPodcastId(
                           podcast.Id,
                           x => x.Release >= recentCutoff && !x.Ignored && !x.Removed))
        {
            ct.ThrowIfCancellationRequested();

            recentEpisodes.Add(new RecentEpisodeEntry
            {
                PodcastId = episode.PodcastId,
                PodcastName = episode.PodcastName,
                EpisodeId = episode.Id,
                EpisodeTitle = episode.Title,
                EpisodeDescription = episode.Description,
                Release = episode.Release,
                Urls = episode.Urls,
                Length = episode.Length,
                Subjects = episode.Subjects,
                Images = episode.Images,
                Language = episode.Language
            });
        }

        return recentEpisodes;
    }

    private async Task<HomePageCache> ResolveHomePageCache(Task recentEpisodesTask, CancellationToken ct)
    {
        var homePageCache = await lookupRepository.GetHomePageCache() ?? new HomePageCache();
        var isRefreshWindow = IsRefreshWindow();
        var shouldRefreshDuration = isRefreshWindow || homePageCache.TotalDuration == default;
        var shouldRefreshCount = isRefreshWindow || homePageCache.ActiveEpisodeCount == null;

        Task<List<TimeSpan>>? durationEpisodesTask = null;
        Task<List<Guid>>? countEpisodesTask = null;

        if (shouldRefreshDuration)
        {
            durationEpisodesTask = episodeRepository
                .GetAllBy(
                    x => !x.Removed && !x.Ignored && (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false ||
                                                      x.PodcastRemoved == null),
                    x => x.Length)
                .ToListAsync(ct)
                .AsTask();
        }

        if (shouldRefreshCount)
        {
            countEpisodesTask = episodeRepository
                .GetAllBy(
                    x => !x.Removed && (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false ||
                                        x.PodcastRemoved == null),
                    x => x.Id)
                .ToListAsync(ct)
                .AsTask();
        }

        await Task.WhenAll(
            [
                recentEpisodesTask,
                durationEpisodesTask ?? Task.CompletedTask,
                countEpisodesTask ?? Task.CompletedTask
            ]);

        if (durationEpisodesTask != null)
        {
            homePageCache.TotalDuration = TimeSpan.FromTicks(durationEpisodesTask.Result.Sum(x => x.Ticks));
        }

        if (countEpisodesTask != null)
        {
            homePageCache.ActiveEpisodeCount = countEpisodesTask.Result.Count;
        }

        if (durationEpisodesTask != null || countEpisodesTask != null)
        {
            await lookupRepository.SaveHomePageCache(homePageCache);
        }

        return homePageCache;
    }

    private static RecentEpisode ToRecentEpisode(PodcastResult x)
    {
        return new RecentEpisode
        {
            EpisodeId = x.EpisodeId,
            EpisodeDescription = WebUtility.HtmlDecode(x.EpisodeDescription),
            EpisodeTitle = WebUtility.HtmlDecode(x.EpisodeTitle),
            PodcastName = x.PodcastName,
            Release = x.Release,
            Spotify = x.Spotify,
            Apple = x.Apple,
            YouTube = x.YouTube,
            BBC = x.BBC,
            InternetArchive = x.InternetArchive,
            Length = TimeSpan.FromSeconds(Math.Round(x.Length.TotalSeconds)),
            Subjects = x.Subjects != null && x.Subjects.Any() ? x.Subjects : null,
            Image = x.Images?.YouTube ?? x.Images?.Spotify ?? x.Images?.Apple ?? x.Images?.Other,
            Language = NormaliseHomepageLanguage(x.Language)
        };
    }

    /// <summary>
    /// Omit English (and empty) so the homepage payload only carries non-English tags.
    /// </summary>
    private static string? NormaliseHomepageLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var trimmed = language.Trim();
        if (trimmed.StartsWith("en", StringComparison.OrdinalIgnoreCase) &&
            (trimmed.Length == 2 || trimmed[2] is '-' or '_'))
        {
            return null;
        }

        return trimmed;
    }

    private async Task<PodcastResult> Sanitise(PodcastResult podcastResult, SanitiseTimingAggregator? agg)
    {
        Regex? titleRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.TitleRegex))
        {
            titleRegex = new Regex(podcastResult.TitleRegex);
        }

        var subjectKnownTerms = (podcastResult.Subjects ?? [])
            .Select(x => _subjects.SingleOrDefault(y => y.Name == x))
            .SelectMany(x => x?.KnownTerms ?? [])
            .ToArray();

        if (agg is null)
        {
            podcastResult.EpisodeTitle = await textSanitiser.SanitiseTitle(
                podcastResult.EpisodeTitle,
                titleRegex,
                podcastResult.KnownTerms ?? [],
                subjectKnownTerms,
                podcastResult.Language);
        }
        else
        {
            var (title, titleTiming) = await textSanitiser.SanitiseTitleTimed(
                podcastResult.EpisodeTitle,
                titleRegex,
                podcastResult.KnownTerms ?? [],
                subjectKnownTerms,
                podcastResult.Language);
            podcastResult.EpisodeTitle = title;
            agg.AddTitle(titleTiming);
        }

        Regex? descRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            descRegex = new Regex(podcastResult.DescriptionRegex, Podcast.DescriptionFlags);
        }

        if (agg is null)
        {
            podcastResult.EpisodeDescription =
                textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);
        }
        else
        {
            var descStart = Stopwatch.GetTimestamp();
            podcastResult.EpisodeDescription =
                textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);
            agg.AddDescription(Stopwatch.GetTimestamp() - descStart);
        }

        return podcastResult;
    }

    private async Task<bool> PublishHomepageToR2(HomePageModel homepageContent)
    {
        var request = new PutObjectRequest
        {
            BucketName = _contentOptions.BucketName,
            Key = _contentOptions.HomepageKey,
            ContentBody = JsonSerializer.Serialize(homepageContent, JsonSerializerOptions),
            ContentType = "application/json",
            DisablePayloadSigning = true
        };

        try
        {
            await client.PutObjectAsync(request);
            logger.LogInformation("Completed '{MethodName}'.", nameof(PublishHomepage));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{MethodName} - Failed to upload homepage-content to R2. BucketName: '{BucketName}', Key: '{Key}'.",
                nameof(PublishHomepage), _contentOptions.BucketName, _contentOptions.HomepageKey);
            return false;
        }
    }

    private sealed record HomepageBuildTiming(
        long RecentPodcastsMs,
        long RecentEpisodesMs,
        long CacheMs,
        long SanitiseMs,
        long SanitisePrepSumMs,
        long SanitiseRulesResolveSumMs,
        long SanitiseLowerSumMs,
        long SanitiseUniversalKtSumMs,
        long SanitiseLangKtSumMs,
        long SanitisePodcastKtSumMs,
        long SanitiseSubjectKtSumMs,
        long SanitiseFinishSumMs,
        long SanitiseDescSumMs,
        long SanitiseTitleMaxMs,
        int UniversalKtCount,
        int EnKtCount,
        int EnLowerCount,
        int RecentEpisodeCount,
        int RecentPodcastCount);

    /// <summary>Thread-safe sum of title-sanitise phase ticks across parallel homepage titles.</summary>
    private sealed class SanitiseTimingAggregator
    {
        private long _prepTicks;
        private long _rulesResolveTicks;
        private long _lowerCaseTicks;
        private long _universalKnownTermsTicks;
        private long _languageKnownTermsTicks;
        private long _podcastKnownTermsTicks;
        private long _subjectKnownTermsTicks;
        private long _finishTicks;
        private long _descriptionTicks;
        private long _titleMaxTicks;
        private int _universalKtCount;
        private int _enKtCount;
        private int _enLowerCount;

        public long PrepTicks => Volatile.Read(ref _prepTicks);
        public long RulesResolveTicks => Volatile.Read(ref _rulesResolveTicks);
        public long LowerCaseTicks => Volatile.Read(ref _lowerCaseTicks);
        public long UniversalKnownTermsTicks => Volatile.Read(ref _universalKnownTermsTicks);
        public long LanguageKnownTermsTicks => Volatile.Read(ref _languageKnownTermsTicks);
        public long PodcastKnownTermsTicks => Volatile.Read(ref _podcastKnownTermsTicks);
        public long SubjectKnownTermsTicks => Volatile.Read(ref _subjectKnownTermsTicks);
        public long FinishTicks => Volatile.Read(ref _finishTicks);
        public long DescriptionTicks => Volatile.Read(ref _descriptionTicks);
        public long TitleMaxTicks => Volatile.Read(ref _titleMaxTicks);
        public int UniversalKtCount => Volatile.Read(ref _universalKtCount);
        public int EnKtCount => Volatile.Read(ref _enKtCount);
        public int EnLowerCount => Volatile.Read(ref _enLowerCount);

        public void AddTitle(TitleSanitiseTiming timing)
        {
            Interlocked.Add(ref _prepTicks, timing.PrepTicks);
            Interlocked.Add(ref _rulesResolveTicks, timing.RulesResolveTicks);
            Interlocked.Add(ref _lowerCaseTicks, timing.LowerCaseTicks);
            Interlocked.Add(ref _universalKnownTermsTicks, timing.UniversalKnownTermsTicks);
            Interlocked.Add(ref _languageKnownTermsTicks, timing.LanguageKnownTermsTicks);
            Interlocked.Add(ref _podcastKnownTermsTicks, timing.PodcastKnownTermsTicks);
            Interlocked.Add(ref _subjectKnownTermsTicks, timing.SubjectKnownTermsTicks);
            Interlocked.Add(ref _finishTicks, timing.FinishTicks);

            // Prefer English term counts (null lang → en); keep the largest seen.
            if (timing.LanguageKnownTermCount >= Volatile.Read(ref _enKtCount))
            {
                Interlocked.Exchange(ref _enKtCount, timing.LanguageKnownTermCount);
            }

            if (timing.LowerCaseTermCount >= Volatile.Read(ref _enLowerCount))
            {
                Interlocked.Exchange(ref _enLowerCount, timing.LowerCaseTermCount);
            }

            if (timing.UniversalKnownTermCount >= Volatile.Read(ref _universalKtCount))
            {
                Interlocked.Exchange(ref _universalKtCount, timing.UniversalKnownTermCount);
            }

            var total = timing.TotalTicks;
            long current;
            while (total > (current = Volatile.Read(ref _titleMaxTicks)) &&
                   Interlocked.CompareExchange(ref _titleMaxTicks, total, current) != current)
            {
            }
        }

        public void AddDescription(long ticks) => Interlocked.Add(ref _descriptionTicks, ticks);
    }

    private sealed record PodcastEntry
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string TitleRegex { get; init; } = string.Empty;
        public string DescriptionRegex { get; init; } = string.Empty;
        public string[]? KnownTerms { get; init; }
    }

    private sealed record RecentEpisodeEntry
    {
        public Guid PodcastId { get; init; }
        public string? PodcastName { get; init; }
        public Guid EpisodeId { get; init; }
        public string EpisodeTitle { get; init; } = string.Empty;
        public string EpisodeDescription { get; init; } = string.Empty;
        public DateTime Release { get; init; }
        public ServiceUrls Urls { get; init; } = new();
        public TimeSpan Length { get; init; }
        public List<string> Subjects { get; init; } = [];
        public EpisodeImages? Images { get; init; }
        public string? Language { get; init; }
    }
}
