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
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
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
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ContentOptions _contentOptions = contentOptions.Value;
    private readonly IReadOnlyList<Subject> _subjects = subjectsProvider.GetAll().ToBlockingEnumerable().ToList();

    public async Task<PublishHomepageResult> PublishHomepage()
    {
        var homepageContent = await GetHomePage(CancellationToken.None);
        var homepagePublished = await PublishHomepageToR2(homepageContent);
        return new PublishHomepageResult(homepagePublished);
    }

    private static bool IsRefreshWindow()
    {
        var utcNow = DateTime.UtcNow;
        return utcNow is { DayOfWeek: DayOfWeek.Monday, Hour: 0, Minute: < 20 };
    }

    private async Task<HomePageModel> GetHomePage(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var recentCutoff = DateTime.UtcNow.AddDays(-7);

        var recentPodcastsTask = podcastRepository
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
            .ToListAsync(ct)
            .AsTask();

        var recentEpisodesTask = GetRecentEpisodes(recentPodcastsTask, recentCutoff, ct); // pragma: allowlist secret

        var homePageCache = await ResolveHomePageCache(recentEpisodesTask, ct); // pragma: allowlist secret
        var activeEpisodeCount = homePageCache.ActiveEpisodeCount ?? 0;

        var recentEpisodes = recentEpisodesTask.Result; // pragma: allowlist secret
        var podcasts = recentPodcastsTask.Result.ToDictionary(x => x.Id);

        var orderedPodcasts = recentEpisodes // pragma: allowlist secret
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
                    Ids = episode.Ids,
                    Services = episode.Services,
                    Length = episode.Length,
                    Subjects = episode.Subjects.Count > 0 ? episode.Subjects.ToArray() : null,
                    Images = episode.Images,
                    KnownTerms = podcast?.KnownTerms,
                    Language = episode.Language
                };
            })
            .OrderByDescending(x => x.Release)
            .ToList();

        var sanitizedPodcasts = await Task.WhenAll(orderedPodcasts.Select(Sanitise));

        return new HomePageModel
        {
            EpisodeCount = activeEpisodeCount,
            RecentEpisodes = sanitizedPodcasts.Select(ToRecentEpisode), // pragma: allowlist secret
            TotalDuration = homePageCache.TotalDuration
        };
    }

    private async Task<List<RecentEpisodeEntry>> GetRecentEpisodes( // pragma: allowlist secret
        Task<List<PodcastEntry>> recentPodcastsTask,
        DateTime recentCutoff,
        CancellationToken ct)
    {
        var podcasts = await recentPodcastsTask;
        var episodeLists = await Task.WhenAll(podcasts.Select(podcast => LoadRecentEpisodes(podcast, recentCutoff, ct))); // pragma: allowlist secret
        return episodeLists.SelectMany(x => x).ToList();
    }

    private async Task<List<RecentEpisodeEntry>> LoadRecentEpisodes( // pragma: allowlist secret
        PodcastEntry podcast,
        DateTime recentCutoff,
        CancellationToken ct)
    {
        var recentEpisodes = new List<RecentEpisodeEntry>(); // pragma: allowlist secret

        await foreach (var episode in episodeRepository.GetByPodcastId(
                           podcast.Id,
                           x => x.Release >= recentCutoff && !x.Ignored && !x.Removed))
        {
            ct.ThrowIfCancellationRequested();
            EpisodeServicePresence.NormalizeCatalog(episode); // pragma: allowlist secret

            recentEpisodes.Add(new RecentEpisodeEntry // pragma: allowlist secret
            {
                PodcastId = episode.PodcastId,
                PodcastName = episode.PodcastName,
                EpisodeId = episode.Id,
                EpisodeTitle = episode.Title,
                EpisodeDescription = episode.Description,
                Release = episode.Release,
                Services = episode.Services,
                Ids = episode.Ids,
                Length = episode.Length,
                Subjects = episode.Subjects,
                Images = EpisodeServicePresence.ToEpisodeImages(episode),
                Language = episode.Language
            });
        }

        return recentEpisodes; // pragma: allowlist secret
    }

    private async Task<HomePageCache> ResolveHomePageCache(Task recentEpisodesTask, CancellationToken ct) // pragma: allowlist secret
    {
        var homePageCache = await lookupRepository.GetHomePageCache() ?? new HomePageCache();
        var isRefreshWindow = IsRefreshWindow();
        var shouldRefreshDuration = isRefreshWindow || homePageCache.TotalDuration == default;
        var shouldRefreshCount = isRefreshWindow || homePageCache.ActiveEpisodeCount == null;

        Task<List<TimeSpan>>? durationEpisodesTask = null; // pragma: allowlist secret
        Task<List<Guid>>? countEpisodesTask = null; // pragma: allowlist secret

        if (shouldRefreshDuration)
        {
            durationEpisodesTask = episodeRepository // pragma: allowlist secret
                .GetAllBy(
                    x => !x.Removed && !x.Ignored && (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false ||
                                                      x.PodcastRemoved == null),
                    x => x.Length)
                .ToListAsync(ct)
                .AsTask();
        }

        if (shouldRefreshCount)
        {
            countEpisodesTask = episodeRepository // pragma: allowlist secret
                .GetAllBy(
                    x => !x.Removed && (!x.PodcastRemoved.IsDefined() || x.PodcastRemoved == false ||
                                        x.PodcastRemoved == null),
                    x => x.Id)
                .ToListAsync(ct)
                .AsTask();
        }

        await Task.WhenAll(
            [
                recentEpisodesTask, // pragma: allowlist secret
                durationEpisodesTask ?? Task.CompletedTask, // pragma: allowlist secret
                countEpisodesTask ?? Task.CompletedTask // pragma: allowlist secret
            ]);

        if (durationEpisodesTask != null) // pragma: allowlist secret
        {
            homePageCache.TotalDuration = TimeSpan.FromTicks(durationEpisodesTask.Result.Sum(x => x.Ticks)); // pragma: allowlist secret
        }

        if (countEpisodesTask != null) // pragma: allowlist secret
        {
            homePageCache.ActiveEpisodeCount = countEpisodesTask.Result.Count; // pragma: allowlist secret
        }

        if (durationEpisodesTask != null || countEpisodesTask != null) // pragma: allowlist secret
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
            Ids = x.Ids,
            Services = x.Services,
            Length = TimeSpan.FromSeconds(Math.Round(x.Length.TotalSeconds)),
            Subjects = x.Subjects != null && x.Subjects.Any() ? x.Subjects : null,
            Image = EpisodeServicePresence.CoalescedImage(x.Services), // pragma: allowlist secret
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

    private async Task<PodcastResult> Sanitise(PodcastResult podcastResult)
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

        podcastResult.EpisodeTitle = await textSanitiser.SanitiseTitle(
            podcastResult.EpisodeTitle,
            titleRegex,
            podcastResult.KnownTerms ?? [],
            subjectKnownTerms,
            podcastResult.Language);

        Regex? descRegex = null;
        if (!string.IsNullOrWhiteSpace(podcastResult.DescriptionRegex))
        {
            descRegex = new Regex(podcastResult.DescriptionRegex, Podcast.DescriptionFlags);
        }

        podcastResult.EpisodeDescription =
            textSanitiser.SanitiseDescription(podcastResult.EpisodeDescription, descRegex);
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
        public Dictionary<string, EpisodeServiceLink>? Services { get; init; } // pragma: allowlist secret
        public EpisodeIds? Ids { get; init; } // pragma: allowlist secret
        public TimeSpan Length { get; init; }
        public List<string> Subjects { get; init; } = [];
        public EpisodeImages? Images { get; init; }
        public string? Language { get; init; }
    }
}
