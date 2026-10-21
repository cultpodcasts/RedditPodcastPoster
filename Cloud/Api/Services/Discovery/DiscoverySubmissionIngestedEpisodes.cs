using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
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
    /// Change state), returns a Succeeded outcome for each accepted result whose episode exists, matched by the
    /// result's Spotify, Apple or YouTube episode id. Returns <c>null</c> in every other case, or when the lookup fails
    /// (logged). Never writes.
    /// </summary>
    Task<DiscoverySubmissionItemOutcome[]?> ResolveMissingOutcomes(
        DiscoverySubmission submission, IReadOnlyList<DiscoveryResultsDocument> reports, CancellationToken c);
}

/// <inheritdoc />
public sealed class DiscoverySubmissionIngestedEpisodes(
    IEpisodeRepository episodeRepository,
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

        var outcomes = keyed
            .Select(x => (x.Result, Episode: episodes.FirstOrDefault(e => Matches(e, x.Ids))))
            .Where(x => x.Episode != null)
            .Select(x => new DiscoverySubmissionItemOutcome
            {
                ResultId = x.Result.Id,
                State = DiscoverySubmissionItemState.Succeeded,
                EpisodeId = x.Episode!.Id,
                PodcastId = x.Episode.PodcastId,
                Message = RecoveredMessage
            })
            .ToArray();
        logger.LogInformation(
            "Submission '{SubmissionId}' is Applied with no item outcomes; resolved {Found} of {Accepted} ingested episodes by platform id.",
            submission.Id, outcomes.Length, results.Count);
        return outcomes;
    }

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
}
