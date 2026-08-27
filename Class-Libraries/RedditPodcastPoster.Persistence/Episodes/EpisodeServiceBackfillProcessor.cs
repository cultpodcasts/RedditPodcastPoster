// pragma: allowlist secret
using Microsoft.Extensions.Logging; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Abstractions.Repositories; // pragma: allowlist secret

namespace RedditPodcastPoster.Persistence.Episodes; // pragma: allowlist secret

/// <summary>
/// Dry-run (default) or apply backfill of <c>services</c> + <c>ids</c> onto episode documents. // pragma: allowlist secret
/// Selection uses raw JSON; apply loads the typed episode and saves only when the shape changes. // pragma: allowlist secret
/// </summary>
public class EpisodeServiceBackfillProcessor( // pragma: allowlist secret
    IEpisodeRepository episodeRepository, // pragma: allowlist secret
    ILogger<EpisodeServiceBackfillProcessor> logger) // pragma: allowlist secret
{
    public async Task<EpisodeServiceBackfillReport> RunAsync( // pragma: allowlist secret
        IReadOnlyList<string> rawDocuments, // pragma: allowlist secret
        bool apply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rawDocuments); // pragma: allowlist secret
        var candidates = EpisodeServiceDocumentMigration.SelectDocumentsToBackfill(rawDocuments); // pragma: allowlist secret
        logger.LogInformation(
            "Episode service backfill: {CandidateCount} document(s) need services/ids. Apply={Apply}.", // pragma: allowlist secret
            candidates.Count,
            apply);

        if (!apply)
        {
            return new EpisodeServiceBackfillReport(candidates.Count, 0, 0, 0, Applied: false); // pragma: allowlist secret
        }

        var saved = 0;
        var unchanged = 0;
        var missing = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var episode = await episodeRepository.GetEpisode(candidate.PodcastId, candidate.EpisodeId); // pragma: allowlist secret
            if (episode is null) // pragma: allowlist secret
            {
                missing++;
                logger.LogWarning(
                    "Episode service backfill: episode {EpisodeId} podcast {PodcastId} was not found; skip.", // pragma: allowlist secret
                    candidate.EpisodeId, // pragma: allowlist secret
                    candidate.PodcastId); // pragma: allowlist secret
                continue;
            }

            if (!EpisodeServiceDocumentMigration.Apply(episode)) // pragma: allowlist secret
            {
                unchanged++;
                continue;
            }

            await episodeRepository.Save(episode); // pragma: allowlist secret
            saved++;
        }

        logger.LogInformation(
            "Episode service backfill apply complete. Saved={Saved}, Unchanged={Unchanged}, Missing={Missing}.", // pragma: allowlist secret
            saved,
            unchanged,
            missing);
        return new EpisodeServiceBackfillReport(candidates.Count, saved, unchanged, missing, Applied: true); // pragma: allowlist secret
    }
}

public readonly record struct EpisodeServiceBackfillReport( // pragma: allowlist secret
    int Candidates,
    int Saved,
    int Unchanged,
    int Missing,
    bool Applied);
