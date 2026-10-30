using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace NewsOrganisationDefaultsBackfill;

/// <summary>
/// One-off: stores <c>indexing.bypassShortEpisodeChecking = true</c> and <c>indexing.alwaysPromoteAsHero = true</c>
/// on every news organisation (removed ones included), the defaults a new news organisation is now created with.
/// Dry-run unless <c>--apply</c>. Each write is a Cosmos partial patch of those two members only (no whole-document
/// upsert), so a concurrent curator or Indexer edit is not overwritten. A news organisation that already has both
/// stored as true is skipped, so a re-run writes nothing. Run only after the release that stops reading an unset
/// flag as on for news, so an older Indexer or Api build never saves over the new values.
/// </summary>
public class NewsOrganisationDefaultsBackfillProcessor(
    INewsOrganisationRepository newsOrganisationRepository,
    ILogger<NewsOrganisationDefaultsBackfillProcessor> logger)
{
    public async Task<NewsOrganisationDefaultsBackfillResult> Run(NewsOrganisationDefaultsBackfillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = new NewsOrganisationDefaultsBackfillResult { Apply = request.Apply };
        await foreach (var organisation in newsOrganisationRepository.GetAll())
        {
            result.Scanned++;
            var indexing = organisation.Indexing;
            if (indexing?.BypassShortEpisodeChecking == true && indexing.AlwaysPromoteAsHero == true)
            {
                result.AlreadySet++;
                continue;
            }

            result.ToPatch++;
            if (indexing is null)
            {
                result.WithoutIndexing++;
            }

            if (indexing?.BypassShortEpisodeChecking == false || indexing?.AlwaysPromoteAsHero == false)
            {
                result.ExplicitFalse.Add(Describe(organisation));
            }

            if (!request.Apply)
            {
                continue;
            }

            var outcome = await newsOrganisationRepository.PatchCreationDefaults(organisation.Id, indexing is not null);
            switch (outcome)
            {
                case NewsOrganisationDefaultsPatchResult.Patched:
                    result.Patched++;
                    break;
                case NewsOrganisationDefaultsPatchResult.NotFound:
                    result.NotFound++;
                    break;
                default:
                    result.ShapeChanged.Add(Describe(organisation));
                    break;
            }
        }

        Report(result);
        return result;
    }

    private static string Describe(NewsOrganisation organisation) => $"{organisation.Name} ({organisation.Id})";

    private void Report(NewsOrganisationDefaultsBackfillResult result)
    {
        logger.LogInformation(
            "{mode}: scanned {scanned} news organisations; {alreadySet} already have both flags true; {toPatch} {verb} " +
            "(of which {withoutIndexing} have no indexing block and {explicitFalse} have a flag stored as false).",
            result.Apply ? "Apply" : "Dry-run",
            result.Scanned,
            result.AlreadySet,
            result.ToPatch,
            result.Apply ? "to patch" : "would be patched",
            result.WithoutIndexing,
            result.ExplicitFalse.Count);
        foreach (var name in result.ExplicitFalse)
        {
            logger.LogInformation("Stored false, set to true: {organisation}", name);
        }

        if (!result.Apply)
        {
            logger.LogInformation("Dry-run only: nothing written. Re-run with --apply to write.");
            return;
        }

        logger.LogInformation(
            "Patched {patched}; not found (deleted since read) {notFound}; indexing shape changed since read {shapeChanged}.",
            result.Patched, result.NotFound, result.ShapeChanged.Count);
        foreach (var name in result.ShapeChanged)
        {
            logger.LogWarning("Not written, indexing changed since read (re-run to retry): {organisation}", name);
        }
    }
}

public sealed class NewsOrganisationDefaultsBackfillResult
{
    public bool Apply { get; init; }
    public int Scanned { get; set; }
    public int AlreadySet { get; set; }
    public int ToPatch { get; set; }
    public int WithoutIndexing { get; set; }
    public List<string> ExplicitFalse { get; } = [];
    public int Patched { get; set; }
    public int NotFound { get; set; }
    public List<string> ShapeChanged { get; } = [];

    /// <summary>0 when done; 2 when an apply run left documents unwritten because they changed (re-run).</summary>
    public int ExitCode => Apply && ShapeChanged.Count > 0 ? 2 : 0;
}
