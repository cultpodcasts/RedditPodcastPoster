using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.Merging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Merging;

/// <summary>
/// Merges incoming catalogue episodes into stored episodes. When more than one stored episode passes
/// <see cref="IEpisodePlatformMatcher"/>, <see cref="IEpisodeMergeCandidateScorer"/> may pick a clear winner;
/// otherwise the incoming episode is recorded as failed. A single certain match (platform id, exact title,
/// EpisodeMatchRegex) is never scored; a single heuristic match is scored and, below the minimum, treated as no match.
/// </summary>
public class EpisodeMerger(
    IEpisodePlatformMatcher platformMatcher,
    IEpisodePlatformMerger platformMerger,
    IEpisodeMergeCandidateScorer candidateScorer,
    ILogger<EpisodeMerger> logger) : IEpisodeMerger
{
    public EpisodeMergeResult MergeEpisodes(
        Podcast podcast,
        IEnumerable<Episode> existingEpisodes,
        IEnumerable<Episode> episodesToMerge)
    {
        System.Text.RegularExpressions.Regex? episodeMatchRegex = null;
        if (!string.IsNullOrWhiteSpace(podcast.EpisodeMatchRegex))
        {
            episodeMatchRegex = new System.Text.RegularExpressions.Regex(
                podcast.EpisodeMatchRegex,
                Podcast.EpisodeMatchFlags);
        }

        var existingList = existingEpisodes.ToList();
        var addedEpisodes = new List<Episode>();
        var mergedEpisodes = new List<(Episode Existing, Episode NewDetails)>();
        var failedEpisodes = new List<IEnumerable<Episode>>();
        var failedMerges = new List<FailedEpisodeMerge>();
        var episodesToSave = new List<Episode>();

        foreach (var episodeToMerge in episodesToMerge)
        {
            var classified = existingList
                .Select(x => (Episode: x, Kind: platformMatcher.Classify(
                    x,
                    episodeToMerge,
                    episodeMatchRegex,
                    podcast,
                    existingList)))
                .Where(x => x.Kind != EpisodeMatchKind.None)
                .ToList();
            var matchingExisting = classified.Select(x => x.Episode).ToList();

            Episode? existingEpisode;
            if (matchingExisting.Count == 0)
            {
                existingEpisode = null;
            }
            else if (matchingExisting.Count == 1)
            {
                existingEpisode = matchingExisting[0];
                if (classified[0].Kind == EpisodeMatchKind.Heuristic)
                {
                    var single = candidateScorer.DecideSingleHeuristic(podcast, episodeToMerge, existingEpisode);
                    if (!single.Accepted)
                    {
                        logger.LogWarning(
                            "Episode merge single heuristic match rejected: podcast '{PodcastName}' ({PodcastId}) incoming '{IncomingTitle}' not merged into '{CandidateTitle}' ({CandidateId}); score {Score} below minimum {MinScore}. Added as a new episode.",
                            podcast.Name,
                            podcast.Id,
                            episodeToMerge.Title,
                            existingEpisode.Title,
                            existingEpisode.Id,
                            single.Score,
                            single.MinScore);
                        existingEpisode = null;
                    }
                }
            }
            else
            {
                var decision = candidateScorer.Decide(podcast, episodeToMerge, matchingExisting);
                if (decision.Winner == null)
                {
                    failedEpisodes.Add(matchingExisting);
                    failedMerges.Add(ToFailedMerge(episodeToMerge, matchingExisting, decision));
                    continue;
                }

                logger.LogInformation(
                    "Episode merge tie-broken: podcast '{PodcastName}' ({PodcastId}) incoming '{IncomingTitle}' merged into '{WinnerTitle}' ({WinnerId}). Candidate scores: {CandidateScores}.",
                    podcast.Name,
                    podcast.Id,
                    episodeToMerge.Title,
                    decision.Winner.Title,
                    decision.Winner.Id,
                    FormatScores(decision.Scores));
                existingEpisode = decision.Winner;
            }

            if (existingEpisode == null)
            {
                episodeToMerge.Id = Guid.NewGuid();
                episodeToMerge.SetPodcastProperties(podcast, inheritLanguageIfUnset: true);
                addedEpisodes.Add(episodeToMerge);
                episodesToSave.Add(episodeToMerge);
                existingList.Add(episodeToMerge);
                continue;
            }

            var updated = platformMerger.MergeInPlace(existingEpisode, episodeToMerge, podcast);
            existingEpisode.SetPodcastProperties(podcast);

            if (updated)
            {
                mergedEpisodes.Add((Existing: existingEpisode, NewDetails: episodeToMerge));
                episodesToSave.Add(existingEpisode);
            }
        }

        return new EpisodeMergeResult(episodesToSave, addedEpisodes, mergedEpisodes, failedEpisodes, failedMerges);
    }

    private static FailedEpisodeMerge ToFailedMerge(
        Episode incoming,
        IReadOnlyList<Episode> candidates,
        EpisodeMergeTieBreakDecision decision)
    {
        return new FailedEpisodeMerge(
            incoming,
            candidates
                .Select(c => new FailedEpisodeMergeCandidate(
                    c,
                    decision.Scores.FirstOrDefault(s => ReferenceEquals(s.Candidate, c))?.Score))
                .ToList(),
            decision.Reason);
    }

    private static string FormatScores(IEnumerable<EpisodeMergeCandidateScore> scores) =>
        string.Join(", ", scores.Select(s => $"'{s.Candidate.Title}' ({s.Candidate.Id})={s.Score}"));
}
