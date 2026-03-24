using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Text;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace RedditPodcastPoster.ContentPublisher;

public class HomepagePublisher(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ITextSanitiser textSanitiser,
    ISubjectsProvider subjectsProvider,
    IAmazonS3 client,
    IOptions<ContentOptions> contentOptions,
    ILookupRepositoryV2 lookupRepository,
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
        var recentEpisodesTask = episodeRepository
            .GetAllBy(
                x => !x.Removed && !x.Ignored && x.Release >= recentCutoff && (!x.PodcastRemoved.IsDefined() ||
                                                                               x.PodcastRemoved == false ||
                                                                               x.PodcastRemoved == null),
                x => new
                {
                    x.PodcastId,
                    x.PodcastName,
                    EpisodeId = x.Id,
                    EpisodeTitle = x.Title,
                    EpisodeDescription = x.Description,
                    x.Release,
                    x.Urls,
                    x.Length,
                    x.Subjects,
                    x.Images
                })
            .ToListAsync(ct)
            .AsTask();

        var homePageCache = await ResolveHomePageCache(recentEpisodesTask, ct);
        var activeEpisodeCount = homePageCache.ActiveEpisodeCount ?? 0;

        var recentEpisodes = recentEpisodesTask.Result;
        var recentPodcastIds = recentEpisodes
            .Select(x => x.PodcastId)
            .Distinct()
            .ToArray();
        var podcasts = recentPodcastIds.Length == 0
            ? new Dictionary<Guid, PodcastEntry>()
            : (await podcastRepository
                .GetAllBy(
                    x => Enumerable.Contains(recentPodcastIds, x.Id),
                    x => new PodcastEntry
                    {
                        Id = x.Id,
                        Name = x.Name,
                        TitleRegex = x.TitleRegex,
                        DescriptionRegex = x.DescriptionRegex,
                        KnownTerms = x.KnownTerms
                    })
                .ToListAsync(ct))
            .ToDictionary(x => x.Id);

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
                    KnownTerms = podcast?.KnownTerms
                };
            })
            .OrderByDescending(x => x.Release)
            .ToList();

        var sanitizedPodcasts = await Task.WhenAll(orderedPodcasts.Select(Sanitise));

        return new HomePageModel
        {
            EpisodeCount = activeEpisodeCount,
            RecentEpisodes = sanitizedPodcasts.Select(ToRecentEpisode),
            TotalDuration = homePageCache.TotalDuration
        };
    }

    private async Task<HomePageCache> ResolveHomePageCache(Task recentEpisodesTask, CancellationToken ct)
    {
        var homePageCache = await lookupRepository.GetHomePageCache() ?? new HomePageCache();
        var shouldRefreshDuration = IsRefreshWindow() || homePageCache.TotalDuration == default;
        var shouldRefreshCount = homePageCache.ActiveEpisodeCount == null;

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
            Image = x.Images?.YouTube ?? x.Images?.Spotify ?? x.Images?.Apple ?? x.Images?.Other
        };
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
            subjectKnownTerms);

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
}