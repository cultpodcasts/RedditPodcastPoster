using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    IEpisodePlatformMatcher platformMatcher,
    ICatalogueCandidatePolicy candidatePolicy,
    ISubjectMatcher subjectMatcher,
    IAsyncInstance<ITitleCasingRulesProvider> titleCasingRulesProvider,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
    {
        if (request is { PodcastAppleId: { } podcastAppleId, EpisodeAppleId: { } episodeAppleId })
        {
            var direct = await applePodcastService.GetEpisode(
                new ApplePodcastId(podcastAppleId), episodeAppleId, indexingContext);
            if (direct != null && direct.Id == episodeAppleId)
            {
                return direct;
            }
        }

        if (!request.PodcastAppleId.HasValue)
        {
            logger.LogInformation(
                "Podcast '{RequestPodcastName}' cannot be found on Apple Podcasts.", request.PodcastName);
        }

        var source = new AppleCandidateSource(request, indexingContext, applePodcastService);
        return await candidatePolicy.FindMatch(
            request.Released,
            source,
            candidates => MatchCandidates(request, candidates, reducer));
    }

    private async Task<AppleEpisode?> MatchCandidates(
        FindAppleEpisodeRequest request,
        IReadOnlyList<AppleEpisode> podcastEpisodes,
        Func<AppleEpisode, bool>? reducer)
    {
        var probe = CreateProbeEpisode(request);
        var candidates = podcastEpisodes.Select(ToCatalogueEpisode).ToList();
        Func<Episode, bool>? episodeReducer = reducer == null
            ? null
            : e =>
            {
                var source = podcastEpisodes.FirstOrDefault(x =>
                    x.Id == EpisodeServicePresence.AppleEpisodeId(e));
                return source != null && reducer(source);
            };

        var mergedIgnored = await MergeIgnoredSubjectsAsync(request.IgnoredSubjects, request.Language);

        if (request.EnrichingYouTubeDiscoveredEpisode)
        {
            await ClassifySubjectsAsync(
                probe,
                candidates,
                request.DefaultSubject,
                mergedIgnored);
        }

        var match = platformMatcher.FindCatalogueMatchByLength(
            probe,
            candidates,
            CreateLookupPodcast(request),
            episodeMatchRegex: null,
            new CatalogueMatchByLengthOptions(
                request.ReleaseAuthority,
                AcceptUniqueDurationWithoutTitleMatch: false,
                request.EnrichingYouTubeDiscoveredEpisode,
                request.DefaultSubject,
                mergedIgnored),
            episodeReducer);

        return match == null
            ? null
            : podcastEpisodes.FirstOrDefault(x => x.Id == EpisodeServicePresence.AppleEpisodeId(match));
    }

    /// <summary>
    /// Apple adapter: the show feed is the (cached) podcast episode list. Apple has no episode title
    /// search, so the search fallback is the same full list (episodes older than the recent page).
    /// </summary>
    private sealed class AppleCandidateSource(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        ICachedApplePodcastService service) : ICatalogueCandidateSource<AppleEpisode>
    {
        public bool HasShow => request.PodcastAppleId.HasValue;

        public async Task<IReadOnlyList<AppleEpisode>> GetRecentShowEpisodes(int limit) =>
            (await GetAll()).OrderByDescending(x => x.Release).Take(limit).ToList();

        public async Task<IReadOnlyList<AppleEpisode>> SearchEpisodes() => await GetAll();

        public string GetId(AppleEpisode item) => item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private async Task<IReadOnlyList<AppleEpisode>> GetAll()
        {
            if (request.PodcastAppleId is not { } podcastAppleId)
            {
                return [];
            }

            var episodes = await service.GetEpisodes(new ApplePodcastId(podcastAppleId), indexingContext);
            return episodes?.ToList() ?? [];
        }
    }

    private async Task ClassifySubjectsAsync(
        Episode probe,
        IList<Episode> candidates,
        string? defaultSubject,
        IReadOnlyList<string>? ignoredSubjects)
    {
        var options = new SubjectEnrichmentOptions(
            IgnoredAssociatedSubjects: null,
            IgnoredSubjects: ignoredSubjects?.ToArray(),
            DefaultSubject: defaultSubject,
            DescriptionRegex: string.Empty);

        probe.Subjects = await MatchSubjectNamesAsync(probe, options);
        foreach (var candidate in candidates)
        {
            candidate.Subjects = await MatchSubjectNamesAsync(candidate, options);
        }
    }

    private async Task<IReadOnlyList<string>?> MergeIgnoredSubjectsAsync(
        IReadOnlyList<string>? podcastIgnored,
        string? language)
    {
        var provider = await titleCasingRulesProvider.GetAsync();
        var languageIgnored = await provider.GetIgnoredSubjectsAsync(language);
        return SubjectEnrichmentOptionsFactory.UnionIgnoreLists(podcastIgnored, languageIgnored);
    }

    private async Task<List<string>> MatchSubjectNamesAsync(
        Episode episode,
        SubjectEnrichmentOptions options)
    {
        var matches = await subjectMatcher.MatchSubjects(episode, options);
        return matches
            .Select(x => x.Subject.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Episode CreateProbeEpisode(FindAppleEpisodeRequest request) =>
        new()
        {
            Title = WebUtility.HtmlDecode(request.EpisodeTitle.Trim()),
            Description = request.EpisodeDescription?.Trim() ?? string.Empty,
            Length = request.EpisodeLength ?? TimeSpan.Zero,
            ReleaseUtc = request.Released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(AppleEpisode episode)
    {
        var mapped = new Episode
        {
            Title = WebUtility.HtmlDecode(episode.Title.Trim()),
            Description = episode.Description ?? string.Empty,
            Length = episode.Duration,
            ReleaseUtc = episode.Release
        };
        EpisodeServicePresence.SetAppleIdentity(mapped, episode.Id);
        return mapped;
    }

    private static Podcast CreateLookupPodcast(FindAppleEpisodeRequest request) =>
        new()
        {
            ReleaseAuthority = request.ReleaseAuthority ?? Service.Apple,
            YouTubePublicationOffset = request.YouTubePublishingDelay?.Ticks
        };
}
