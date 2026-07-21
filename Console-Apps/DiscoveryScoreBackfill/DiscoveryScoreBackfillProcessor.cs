using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Discovery.ML.Configuration;
using RedditPodcastPoster.Discovery.ML.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace DiscoveryScoreBackfill;

public sealed class DiscoveryScoreBackfillProcessor(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoveryResultScorer discoveryResultScorer,
    IOptions<DiscoveryScorerSettings> scorerSettings,
    ILogger<DiscoveryScoreBackfillProcessor> logger)
{
    public async Task<int> Run(DiscoveryScoreBackfillRequest request, string evidencePath, CancellationToken cancellationToken = default)
    {
        if (!discoveryResultScorer.IsEnabled)
        {
            logger.LogError("Discovery scorer is not enabled or model files could not be loaded.");
            return 1;
        }

        var documents = await LoadDocuments(request, cancellationToken).ToListAsync(cancellationToken);
        if (documents.Count == 0)
        {
            logger.LogWarning("No discovery documents matched the request.");
            return 0;
        }

        logger.LogInformation("Scoring {DocumentCount} discovery document(s)...", documents.Count);

        var scoredRows = new List<ScoredDiscoveryResult>();
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var results = document.DiscoveryResults?.ToList() ?? [];
            logger.LogInformation(
                "Document {DocumentId} ({DiscoveryBegan:O}): scoring {ResultCount} result(s)...",
                document.Id,
                document.DiscoveryBegan,
                results.Count);

            foreach (var result in results)
            {
                var score = discoveryResultScorer.Score(result);
                result.AcceptProbability = score.AcceptProbability;
                result.AutoHidden = score.ShouldAutoHide;
                scoredRows.Add(new ScoredDiscoveryResult(document.Id, document.DiscoveryBegan, result));
            }

            document.DiscoveryResults = results;

            if (request.DryRun)
            {
                logger.LogInformation("Dry-run: skipping save for document {DocumentId}.", document.Id);
                continue;
            }

            await discoveryResultsRepository.Save(document);
            logger.LogInformation("Saved document {DocumentId}. State remains {State}.", document.Id, document.State);
        }

        var settings = scorerSettings.Value;
        var manifestPath = ResolveManifestPath(settings);
        var report = DiscoveryScoreBackfillAnalyzer.BuildEvidenceReport(
            documents,
            scoredRows,
            request.DryRun,
            settings.AutoHideThreshold,
            manifestPath);

        var evidenceDirectory = Path.GetDirectoryName(evidencePath);
        if (!string.IsNullOrWhiteSpace(evidenceDirectory))
        {
            Directory.CreateDirectory(evidenceDirectory);
        }

        await File.WriteAllTextAsync(evidencePath, report, cancellationToken);
        logger.LogInformation("Wrote evidence report to {EvidencePath}.", evidencePath);
        Console.WriteLine(report);

        return 0;
    }

    private async IAsyncEnumerable<DiscoveryResultsDocument> LoadDocuments(
        DiscoveryScoreBackfillRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var documentIds = request.DocumentIds?.Distinct().ToArray();
        if (documentIds is { Length: > 0 })
        {
            await foreach (var document in discoveryResultsRepository.GetByIds(documentIds).WithCancellation(cancellationToken))
            {
                yield return document;
            }

            yield break;
        }

        if (request.AllUnprocessed)
        {
            await foreach (var document in discoveryResultsRepository.GetAllUnprocessed().WithCancellation(cancellationToken))
            {
                yield return document;
            }
        }
    }

    private static string? ResolveManifestPath(DiscoveryScorerSettings settings)
    {
        var directory = settings.ModelDirectory;
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        return Path.Combine(Path.GetFullPath(directory), settings.ManifestFileName);
    }
}
