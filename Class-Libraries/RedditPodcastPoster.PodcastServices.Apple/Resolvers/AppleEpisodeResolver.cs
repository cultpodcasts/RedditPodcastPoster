using System.Net;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Resolvers;

public class AppleEpisodeResolver(
    ICachedApplePodcastService applePodcastService,
    IEpisodePlatformMatcher platformMatcher,
    ISubjectMatcher subjectMatcher,
    ILogger<AppleEpisodeResolver> logger)
    : IAppleEpisodeResolver
{
    public async Task<AppleEpisode?> FindEpisode(
        FindAppleEpisodeRequest request,
        IndexingContext indexingContext,
        Func<AppleEpisode, bool>? reducer = null)
    {
        AppleEpisode? matchingEpisode = null;
        IEnumerable<AppleEpisode>? podcastEpisodes = null;
        if (request.PodcastAppleId.HasValue)
        {
            var applePodcastId = new ApplePodcastId(request.PodcastAppleId.Value);
            if (request.EpisodeAppleId.HasValue)
            {
                var episode =
                    await applePodcastService.GetEpisode(applePodcastId, request.EpisodeAppleId.Value, indexingContext);
                if (episode != null)
                {
                    podcastEpisodes = [episode];
                }
            }
            else
            {
                podcastEpisodes = await applePodcastService.GetEpisodes(applePodcastId, indexingContext);
            }
        }

        if (request.EpisodeAppleId != null && podcastEpisodes != null)
        {
            matchingEpisode = podcastEpisodes.FirstOrDefault(x => x.Id == request.EpisodeAppleId);
        }

        if (matchingEpisode == null && podcastEpisodes != null && request.PodcastAppleId.HasValue)
        {
            var probe = CreateProbeEpisode(request);
            var candidates = podcastEpisodes.Select(ToCatalogueEpisode).ToList();
            Func<Episode, bool>? episodeReducer = reducer == null
                ? null
                : e =>
                {
                    var source = podcastEpisodes.FirstOrDefault(x => x.Id == e.AppleId);
                    return source != null && reducer(source);
                };

            if (request.EnrichingYouTubeDiscoveredEpisode)
            {
                await ClassifySubjectsAsync(
                    probe,
                    candidates,
                    request.DefaultSubject,
                    request.IgnoredSubjects);
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
                    request.IgnoredSubjects),
                episodeReducer);

            matchingEpisode = match == null
                ? null
                : podcastEpisodes.FirstOrDefault(x => x.Id == match.AppleId);
        }
        else if (matchingEpisode == null && podcastEpisodes != null && !request.PodcastAppleId.HasValue)
        {
            logger.LogInformation(
                "Podcast '{RequestPodcastName}' cannot be found on Apple Podcasts.", request.PodcastName);
        }

        return matchingEpisode;
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
            Release = request.Released ?? DateTime.MinValue
        };

    private static Episode ToCatalogueEpisode(AppleEpisode episode) =>
        new()
        {
            Title = WebUtility.HtmlDecode(episode.Title.Trim()),
            Description = episode.Description ?? string.Empty,
            Length = episode.Duration,
            Release = episode.Release,
            AppleId = episode.Id
        };

    private static Podcast CreateLookupPodcast(FindAppleEpisodeRequest request) =>
        new()
        {
            ReleaseAuthority = request.ReleaseAuthority ?? Service.Apple,
            YouTubePublicationOffset = request.YouTubePublishingDelay?.Ticks
        };
}
