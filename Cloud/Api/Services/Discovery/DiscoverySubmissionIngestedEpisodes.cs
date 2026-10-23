using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace Api.Services.Discovery;

/// <summary>
/// Read-only fallback for the "Ingested episodes" view of a submission whose outcome was set by hand.
/// </summary>
public interface IDiscoverySubmissionIngestedEpisodes
{
    /// <summary>
    /// For an <see cref="DiscoverySubmissionStatus.Applied"/> submission with no recorded item outcomes (recovered via
    /// Change state), returns a Succeeded outcome for each accepted result whose episode, news report, or TV episode
    /// exists, matched by the result's Spotify, Apple or YouTube episode id. News reports and TV episodes set
    /// <see cref="DiscoverySubmissionItemOutcome.ContentKind"/> and use the item id / parent id, matching submit.
    /// Returns <c>null</c> in every other case, or when the episode lookup fails (logged). Never writes.
    /// </summary>
    Task<DiscoverySubmissionItemOutcome[]?> ResolveMissingOutcomes(
        DiscoverySubmission submission, IReadOnlyList<DiscoveryResultsDocument> reports, CancellationToken c);
}

/// <inheritdoc />
public sealed class DiscoverySubmissionIngestedEpisodes(
    IEpisodeRepository episodeRepository,
    INewsReportRepository newsReportRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    ILogger<DiscoverySubmissionIngestedEpisodes> logger) : IDiscoverySubmissionIngestedEpisodes
{
    public const string RecoveredMessage = "Recovered";

    public async Task<DiscoverySubmissionItemOutcome[]?> ResolveMissingOutcomes(
        DiscoverySubmission submission, IReadOnlyList<DiscoveryResultsDocument> reports, CancellationToken c)
    {
        try
        {
            return await Resolve(submission, reports, c);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort view: never hide the submission because the lookup failed.
            logger.LogError(ex, "Failure resolving ingested episodes for recovered submission '{SubmissionId}'.",
                submission.Id);
            return null;
        }
    }

    private async Task<DiscoverySubmissionItemOutcome[]?> Resolve(
        DiscoverySubmission submission, IReadOnlyList<DiscoveryResultsDocument> reports, CancellationToken c)
    {
        if (submission.Status != DiscoverySubmissionStatus.Applied || submission.ItemOutcomes is { Length: > 0 })
        {
            return null;
        }

        var accepted = (submission.SupervisorAcceptedResultIds ?? submission.SubmitterAcceptedResultIds).ToHashSet();
        var results = reports.SelectMany(x => x.DiscoveryResults).Where(x => accepted.Contains(x.Id)).ToList();
        if (results.Count == 0)
        {
            return null;
        }

        var keyed = results.Select(r => (Result: r, Ids: Ids(r.Urls))).ToList();
        var spotify = keyed.Select(x => x.Ids.Spotify).OfType<string>().Distinct().ToArray();
        var apple = keyed.Select(x => x.Ids.Apple).OfType<long>().Distinct().ToArray();
        var youTube = keyed.Select(x => x.Ids.YouTube).OfType<string>().Distinct().ToArray();
        var episodes = new List<Episode>();
        if (spotify.Length > 0)
        {
            episodes.AddRange(await episodeRepository
                .GetAllBy(e => e.Ids != null && Enumerable.Contains(spotify, e.Ids.Spotify)).ToListAsync(c));
        }

        if (apple.Length > 0)
        {
            episodes.AddRange(await episodeRepository
                .GetAllBy(e => e.Ids != null && e.Ids.Apple != null && Enumerable.Contains(apple, e.Ids.Apple.Value))
                .ToListAsync(c));
        }

        if (youTube.Length > 0)
        {
            episodes.AddRange(await episodeRepository
                .GetAllBy(e => e.Ids != null && Enumerable.Contains(youTube, e.Ids.YouTube)).ToListAsync(c));
        }

        var spotifyUrls = SpotifyUrlCandidates(keyed);
        var appleUrls = AppleUrlCandidates(keyed);
        var youTubeUrls = YouTubeUrlCandidates(keyed);
        var news = await FindPlayables(newsReportRepository, submission.Id, spotifyUrls, appleUrls, youTubeUrls, c);
        var television = await FindPlayables(
            tvShowEpisodeRepository, submission.Id, spotifyUrls, appleUrls, youTubeUrls, c);

        var outcomes = new List<DiscoverySubmissionItemOutcome>();
        foreach (var item in keyed)
        {
            var episode = episodes.FirstOrDefault(e => Matches(e, item.Ids));
            if (episode != null)
            {
                outcomes.Add(Recovered(item.Result.Id, episode.Id, episode.PodcastId, contentKind: null));
                continue;
            }

            var report = news.FirstOrDefault(playable => ServiceMatches(playable.Services, item.Ids));
            if (report != null)
            {
                outcomes.Add(Recovered(
                    item.Result.Id, report.Id, report.NewsOrganisationId, nameof(ContentKind.NewsReport)));
                continue;
            }

            var tvEpisode = television.FirstOrDefault(playable => ServiceMatches(playable.Services, item.Ids));
            if (tvEpisode != null)
            {
                outcomes.Add(Recovered(
                    item.Result.Id, tvEpisode.Id, tvEpisode.TvShowId, nameof(ContentKind.TvShowEpisode)));
            }
        }

        logger.LogInformation(
            "Submission '{SubmissionId}' is Applied with no item outcomes; resolved {Found} of {Accepted} ingested episodes by platform id.",
            submission.Id, outcomes.Count, results.Count);
        return outcomes.ToArray();
    }

    /// <summary>
    /// Catalogue containers store the service id inside the service URL. A failed catalogue query must not hide
    /// podcast episodes already resolved for the same submission.
    /// </summary>
    private async Task<List<TPlayable>> FindPlayables<TPlayable>(
        IFilterableRepository<TPlayable> repository,
        Guid submissionId,
        Uri[] spotifyUrls,
        Uri[] appleUrls,
        Uri[] youTubeUrls,
        CancellationToken c) where TPlayable : Playable
    {
        var found = new List<TPlayable>();
        if (spotifyUrls.Length > 0)
        {
            found.AddRange(await Query(repository, submissionId, item =>
                item.Services != null &&
                item.Services[ServiceKeys.Spotify].Url != null &&
                Enumerable.Contains(spotifyUrls, item.Services[ServiceKeys.Spotify].Url), c));
        }

        if (appleUrls.Length > 0)
        {
            found.AddRange(await Query(repository, submissionId, item =>
                item.Services != null &&
                item.Services[ServiceKeys.Apple].Url != null &&
                Enumerable.Contains(appleUrls, item.Services[ServiceKeys.Apple].Url), c));
        }

        if (youTubeUrls.Length > 0)
        {
            found.AddRange(await Query(repository, submissionId, item =>
                item.Services != null &&
                item.Services[ServiceKeys.YouTube].Url != null &&
                Enumerable.Contains(youTubeUrls, item.Services[ServiceKeys.YouTube].Url), c));
        }

        return found.DistinctBy(item => item.Id).ToList();
    }

    private async Task<List<TPlayable>> Query<TPlayable>(
        IFilterableRepository<TPlayable> repository,
        Guid submissionId,
        Expression<Func<TPlayable, bool>> selector,
        CancellationToken c) where TPlayable : Playable
    {
        try
        {
            return await repository.GetAllBy(selector).ToListAsync(c);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failure resolving {PlayableType} for recovered submission '{SubmissionId}'.",
                typeof(TPlayable).Name, submissionId);
            return [];
        }
    }

    private static DiscoverySubmissionItemOutcome Recovered(
        Guid resultId, Guid itemId, Guid parentId, string? contentKind) =>
        new()
        {
            ResultId = resultId,
            State = DiscoverySubmissionItemState.Succeeded,
            EpisodeId = itemId,
            PodcastId = parentId,
            Message = RecoveredMessage,
            ContentKind = contentKind
        };

    private static (string? Spotify, long? Apple, string? YouTube) Ids(DiscoveryResultUrls urls)
    {
        var spotify = urls.Spotify is { } s ? SpotifyIdResolver.GetEpisodeId(s) : null;
        return (string.IsNullOrWhiteSpace(spotify) ? null : spotify,
            urls.Apple is { } a ? AppleIdResolver.GetEpisodeId(a) : null,
            urls.YouTube is { } y ? YouTubeIdResolver.Extract(y) : null);
    }

    private static bool Matches(Episode episode, (string? Spotify, long? Apple, string? YouTube) ids) =>
        episode.Ids is { } e &&
        ((ids.Spotify != null && e.Spotify == ids.Spotify) ||
         (ids.Apple != null && e.Apple == ids.Apple) ||
         (ids.YouTube != null && e.YouTube == ids.YouTube));

    private static bool ServiceMatches(
        IReadOnlyDictionary<string, ServiceLink>? services,
        (string? Spotify, long? Apple, string? YouTube) ids)
    {
        if (services == null)
        {
            return false;
        }

        if (ids.Spotify != null &&
            services.TryGetValue(ServiceKeys.Spotify, out var spotify) &&
            spotify.Url != null &&
            SpotifyIdResolver.GetEpisodeId(spotify.Url) == ids.Spotify)
        {
            return true;
        }

        if (ids.Apple != null &&
            services.TryGetValue(ServiceKeys.Apple, out var apple) &&
            apple.Url != null &&
            AppleIdResolver.GetEpisodeId(apple.Url) == ids.Apple)
        {
            return true;
        }

        if (ids.YouTube != null &&
            services.TryGetValue(ServiceKeys.YouTube, out var youTube) &&
            youTube.Url != null &&
            YouTubeIdResolver.Extract(youTube.Url) == ids.YouTube)
        {
            return true;
        }

        return false;
    }

    private static Uri[] SpotifyUrlCandidates(
        IReadOnlyList<(DiscoveryResult Result, (string? Spotify, long? Apple, string? YouTube) Ids)> keyed)
    {
        var urls = new HashSet<Uri>();
        foreach (var item in keyed)
        {
            if (item.Ids.Spotify is not { } id)
            {
                continue;
            }

            urls.Add(new Uri($"https://open.spotify.com/episode/{id}"));
            if (item.Result.Urls.Spotify is not { } raw)
            {
                continue;
            }

            urls.Add(raw);
            urls.Add(raw.CleanSpotifyUrl());
        }

        return urls.ToArray();
    }

    private static Uri[] AppleUrlCandidates(
        IReadOnlyList<(DiscoveryResult Result, (string? Spotify, long? Apple, string? YouTube) Ids)> keyed)
    {
        var urls = new HashSet<Uri>();
        foreach (var item in keyed)
        {
            if (item.Result.Urls.Apple is not { } raw || item.Ids.Apple is null)
            {
                continue;
            }

            urls.Add(raw);
            urls.Add(raw.CleanAppleUrl());
            if (AppleIdResolver.GetPodcastId(raw) is not { } showId)
            {
                continue;
            }

            urls.Add(new Uri($"https://podcasts.apple.com/podcast/episode/id{showId}?i={item.Ids.Apple}"));
            urls.Add(new Uri($"https://podcasts.apple.com/us/podcast/episode/id{showId}?i={item.Ids.Apple}"));
        }

        return urls.ToArray();
    }

    private static Uri[] YouTubeUrlCandidates(
        IReadOnlyList<(DiscoveryResult Result, (string? Spotify, long? Apple, string? YouTube) Ids)> keyed)
    {
        var urls = new HashSet<Uri>();
        foreach (var item in keyed)
        {
            if (item.Ids.YouTube is not { } id)
            {
                continue;
            }

            urls.Add(new Uri($"https://www.youtube.com/watch?v={id}"));
            urls.Add(new Uri($"https://youtu.be/{id}"));
            urls.Add(new Uri($"https://www.youtube.com/shorts/{id}"));
            urls.Add(new Uri($"https://www.youtube.com/embed/{id}"));
            urls.Add(new Uri($"https://www.youtube.com/live/{id}"));
            if (item.Result.Urls.YouTube is { } raw)
            {
                urls.Add(raw);
            }
        }

        return urls.ToArray();
    }
}
