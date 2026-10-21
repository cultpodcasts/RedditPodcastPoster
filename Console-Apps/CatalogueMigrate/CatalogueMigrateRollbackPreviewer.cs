using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate;

/// <summary>What a rollback of one podcast would do. <see cref="Refused"/> when the parent has grown.</summary>
public sealed record CatalogueMigrateRollbackPreview(
    Guid ParentId,
    int Stored,
    int Written,
    IReadOnlyList<EpisodeSearchRecord> SearchDocuments)
{
    public bool Refused => Stored > Written;
}

/// <summary>
/// The read-only half of a rollback: counts the destination rows and logs the plan. It needs only
/// <see cref="ICatalogueMigrateDestinationReader"/>, so a rollback preview (no <c>--apply</c>) never builds the
/// applier or the Search client. <see cref="CatalogueMigrateApplier.Rollback"/> uses the same preview.
/// </summary>
public class CatalogueMigrateRollbackPreviewer(
    ICatalogueMigrateDestinationReader reader,
    ILogger<CatalogueMigrateRollbackPreviewer> logger)
{
    public async Task<CatalogueMigrateRollbackPreview> Preview(CatalogueMigrateEvidenceRecord begin, bool apply)
    {
        if (begin.BeforePodcast is not { } podcast || begin.BeforeEpisodes is not { } episodes)
        {
            throw new InvalidDataException($"Begin record for {begin.PodcastId} has no snapshot.");
        }

        var kind = begin.Kind;
        var parentId = begin.DestParentId ?? begin.DestPlayableIds.Single();
        var stored = await reader.CountPlayables(kind, parentId);
        var written = kind == SubmitClassification.Film ? 1 : begin.DestPlayableIds.Length;
        var searchDocuments = episodes
            .Where(episode => !episode.Removed)
            .Select(episode => new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord(includeUnifiedPlayableFields: true))
            .ToList();
        var preview = new CatalogueMigrateRollbackPreview(parentId, stored, written, searchDocuments);

        if (preview.Refused)
        {
            logger.LogError(
                "Catalogue migrate rollback refused for {PodcastId}: {Stored} playables under {ParentId}, batch wrote {Written}.",
                begin.PodcastId,
                stored,
                parentId,
                written);
            return preview;
        }

        logger.LogInformation(
            "Catalogue migrate rollback {Mode} {PodcastId} {Kind}: restore podcast + {EpisodeCount} episodes, {SearchDocumentCount} search documents back to Episode, delete {Written} playables under {ParentId}.",
            apply ? "apply" : "dry-run",
            begin.PodcastId,
            kind,
            episodes.Length,
            searchDocuments.Count,
            written,
            parentId);
        return preview;
    }
}
