using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Discovery;

public class DiscoverySubmissionHoldService(
    IDiscoveryResultsRepository discoveryResultsRepository,
    IDiscoverySubmissionsRepository discoverySubmissionsRepository,
    IDiscoveryResultsService discoveryResultsService,
    TimeProvider timeProvider,
    ILogger<DiscoverySubmissionHoldService> logger) : IDiscoverySubmissionHoldService
{
    public const string SubmittedAction = "Submitted";

    public async Task<DiscoverySubmissionHoldResult> HoldAsync(
        DiscoverySubmitRequest request,
        string submitterSubject,
        IReadOnlyList<string> supervisorSubjects,
        CancellationToken cancellationToken)
    {
        var documentIds = request.DiscoveryResultsDocumentIds.Distinct().ToArray();
        var moved = new List<Guid>();
        Guid? attemptedSubmissionId = null;
        try
        {
            var documents = await discoveryResultsService.GetDocuments(documentIds);
            var unavailable = documentIds
                .Where(id => documents.All(d => d.Id != id || d.State != DiscoveryResultsDocumentState.Unprocessed))
                .ToArray();
            if (documentIds.Length == 0 || unavailable.Length > 0)
            {
                logger.LogWarning("Supervised discovery submit refused; unavailable reports: {DocumentIds}.",
                    string.Join(",", unavailable));
                return new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Conflict,
                    UnavailableDocumentIds: unavailable);
            }

            var knownResultIds = documents.SelectMany(d => d.DiscoveryResults).Select(r => r.Id).ToHashSet();
            var acceptedResultIds = request.ResultIds.Distinct().ToArray();
            var unknownResultIds = acceptedResultIds.Where(id => !knownResultIds.Contains(id)).ToArray();
            if (unknownResultIds.Length > 0)
            {
                // Refuse rather than silently drop: the submitter (often an AI) must learn its input was wrong,
                // and the stored accepted set must be exactly what it sent for the supervisor diff.
                logger.LogWarning("Supervised discovery submit refused; result ids not in the submitted reports: {ResultIds}.",
                    string.Join(",", unknownResultIds));
                return new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.InvalidResultIds,
                    UnknownResultIds: unknownResultIds);
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var submission = new DiscoverySubmission
            {
                Id = Guid.NewGuid(),
                Status = DiscoverySubmissionStatus.Pending,
                SubmitterSubject = submitterSubject,
                SubmittedAt = now,
                DiscoveryResultsDocumentIds = documentIds,
                SubmitterAcceptedResultIds = acceptedResultIds,
                SupervisorSubjects = supervisorSubjects.Count > 0 ? supervisorSubjects.ToArray() : null,
                History = [new DiscoverySubmissionHistoryEntry { At = now, ActorSubject = submitterSubject, Action = SubmittedAction }]
            };

            foreach (var documentId in documentIds)
            {
                if (!await discoveryResultsRepository.TryTransitionState(documentId,
                        DiscoveryResultsDocumentState.Unprocessed, DiscoveryResultsDocumentState.PendingSupervisor,
                        cancellationToken))
                {
                    await Release(moved, cancellationToken);
                    return new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Conflict,
                        UnavailableDocumentIds: [documentId]);
                }

                moved.Add(documentId);
            }

            attemptedSubmissionId = submission.Id;
            await discoverySubmissionsRepository.Save(submission, cancellationToken);
            await discoveryResultsService.UpdateDiscoveryInfoContent();
            logger.LogInformation(
                "Held supervised discovery submission '{SubmissionId}' by '{Submitter}' for {DocumentCount} reports.",
                submission.Id, submitterSubject, documentIds.Length);
            return new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Held, submission.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failure holding supervised discovery submission.");
            if (attemptedSubmissionId is { } submissionId)
            {
                // The save may have committed before throwing; never leave a Pending submission over released reports.
                try
                {
                    await discoverySubmissionsRepository.Delete(submissionId, CancellationToken.None);
                }
                catch (Exception deleteEx)
                {
                    logger.LogError(deleteEx,
                        "Failure deleting orphaned discovery submission '{SubmissionId}'; apply must re-check report state.",
                        submissionId);
                }
            }

            await Release(moved, CancellationToken.None);
            return new DiscoverySubmissionHoldResult(DiscoverySubmissionHoldStatus.Failed);
        }
    }

    private async Task Release(IEnumerable<Guid> documentIds, CancellationToken cancellationToken)
    {
        foreach (var id in documentIds)
        {
            try
            {
                await discoveryResultsRepository.TryTransitionState(id,
                    DiscoveryResultsDocumentState.PendingSupervisor, DiscoveryResultsDocumentState.Unprocessed,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failure releasing report '{DocumentId}' back to Unprocessed.", id);
            }
        }
    }
}
