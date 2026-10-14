using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RedditPodcastPoster.PodcastServices.YouTube.CandidatePreference;

/// <summary>
/// Scores the finder's choice and its validated contenders: penalised title tokens (📱) and static
/// (still-image) uploads lose points. Highest score wins; ties keep the finder's choice, then list order.
/// Thumbnails are fetched only for the candidates passed in (the finder's choice plus at most
/// <see cref="YouTubeCandidatePreferenceOptions.MaxContenders"/>).
/// </summary>
public class YouTubeCandidatePreference(
    IYouTubeThumbnailMotionDetector motionDetector,
    IOptions<YouTubeCandidatePreferenceOptions> options,
    ILogger<YouTubeCandidatePreference> logger) : IYouTubeCandidatePreference
{
    public async Task<string> ChooseAsync(
        YouTubeCandidate chosen,
        IReadOnlyList<YouTubeCandidate> contenders,
        CancellationToken cancellationToken = default)
    {
        if (contenders.Count == 0)
        {
            return chosen.VideoId;
        }

        var settings = options.Value;
        var pool = new[] { chosen }.Concat(contenders.Where(c => c.VideoId != chosen.VideoId)).ToList();
        var motions = await Task.WhenAll(pool.Select(c => motionDetector.DetectAsync(c.VideoId, cancellationToken)));
        var scored = pool
            .Select((candidate, index) => (Candidate: candidate, Motion: motions[index], Score: Score(candidate, motions[index], settings), Index: index))
            .ToList();
        var best = scored.OrderByDescending(x => x.Score).ThenBy(x => x.Index).First();

        logger.LogInformation(
            "YouTube candidate preference scored {candidateCount} candidates: {scores}. Chosen '{videoId}'.",
            scored.Count,
            string.Join(", ", scored.Select(x => $"{x.Candidate.VideoId}={x.Score} ({x.Motion}{(HasPenalisedToken(x.Candidate.Title, settings) ? ", penalised title" : string.Empty)})")),
            best.Candidate.VideoId);
        return best.Candidate.VideoId;
    }

    internal static int Score(YouTubeCandidate candidate, ThumbnailMotion motion, YouTubeCandidatePreferenceOptions settings)
    {
        var score = 0;
        if (HasPenalisedToken(candidate.Title, settings))
        {
            score -= settings.PenalisedTitlePenalty;
        }

        if (motion == ThumbnailMotion.Static)
        {
            score -= settings.StaticImagePenalty;
        }

        return score;
    }

    internal static bool HasPenalisedToken(string? title, YouTubeCandidatePreferenceOptions settings) =>
        !string.IsNullOrEmpty(title) &&
        settings.PenalisedTitleTokens.Any(t => !string.IsNullOrEmpty(t) && title.Contains(t, StringComparison.Ordinal));
}