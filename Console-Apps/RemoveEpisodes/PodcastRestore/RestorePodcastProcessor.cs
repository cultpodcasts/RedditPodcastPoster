using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;

namespace RemoveEpisodes.PodcastRestore;

/// <summary>
///     Undo of an accidental podcast removal. Dry run by default; <c>--non-dry-run</c> persists.
///     Exit codes: 0 success, 1 a side effect failed for at least one podcast (re-run; it is idempotent),
///     2 bad/ambiguous target (nothing written).
/// </summary>
public class RestorePodcastProcessor(
    PodcastTargetResolver targetResolver,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    IShortnerService shortnerService,
    ILogger<RestorePodcastProcessor> logger)
{
    public async Task<int> Process(RestorePodcastRequest request, CancellationToken c = default)
    {
        var podcasts = await targetResolver.Resolve(request.PodcastIds.ToList(), request.PodcastNames.ToList(), c);
        if (podcasts == null)
        {
            return 2;
        }

        var mode = request.IsNonDryRun ? "APPLY" : "DRY RUN";
        var exit = 0;
        foreach (var podcast in podcasts)
        {
            c.ThrowIfCancellationRequested();
            var episodes = await episodeRepository.GetByPodcastId(podcast.Id).ToListAsync(c);
            var plan = PodcastRestorePlan.Create(podcast, episodes);
            Report(mode, plan, request.SkipShortner);

            if (!request.IsNonDryRun || !plan.HasChanges)
            {
                continue;
            }

            if (!await Apply(plan, request.SkipShortner, c))
            {
                exit = 1;
            }
        }

        if (!request.IsNonDryRun)
        {
            logger.LogWarning("[DRY RUN] Nothing was written. Re-run with --non-dry-run to apply.");
        }

        return exit;
    }

    private void Report(string mode, PodcastRestorePlan plan, bool skipShortner)
    {
        var podcast = plan.Podcast;
        logger.LogInformation(
            "[{mode}] Podcast '{name}' ({id}): removed={removed} -> {action}.",
            mode, podcast.Name, podcast.Id, podcast.Removed,
            plan.PodcastNeedsUnremove ? "removed=false" : "unchanged (not removed)");
        logger.LogInformation(
            "[{mode}]   Episodes: {clear} clear parentRemoved; {republish} re-index{shortner}; {left} stay removed (removed before/independently).",
            mode, plan.EpisodesToClearParentRemoved.Count, plan.EpisodesToRepublish.Count,
            skipShortner ? "" : " + re-create short URL", plan.EpisodesLeftRemoved.Count);
        foreach (var e in plan.EpisodesToRepublish)
        {
            logger.LogInformation("[{mode}]   restore  {episodeId} '{title}'", mode, e.Id, e.Title);
        }

        foreach (var e in plan.EpisodesLeftRemoved)
        {
            logger.LogInformation("[{mode}]   keep-removed {episodeId} '{title}'", mode, e.Id, e.Title);
        }
    }

    private async Task<bool> Apply(PodcastRestorePlan plan, bool skipShortner, CancellationToken c)
    {
        var podcast = plan.Podcast;
        if (plan.PodcastNeedsUnremove)
        {
            podcast.Removed = false;
            await podcastRepository.Save(podcast);
        }

        // Same domain projection that stamps parentRemoved on removal (Episode.SetPodcastProperties).
        foreach (var episode in plan.EpisodesToClearParentRemoved)
        {
            c.ThrowIfCancellationRequested();
            episode.SetPodcastProperties(podcast, inheritLanguageIfUnset: false);
            await episodeRepository.Save(episode);
        }

        if (plan.EpisodesToRepublish.Count == 0)
        {
            logger.LogInformation("[APPLY] Restored podcast '{name}' ({id}).", podcast.Name, podcast.Id);
            return true;
        }

        var ok = await Reindex(plan, c);
        if (!skipShortner)
        {
            ok &= await RewriteShortUrls(plan);
        }

        logger.LogInformation("[APPLY] Restored podcast '{name}' ({id}){suffix}.", podcast.Name, podcast.Id,
            ok ? "" : " with side-effect failures; re-run with the same arguments");
        return ok;
    }

    private async Task<bool> Reindex(PodcastRestorePlan plan, CancellationToken c)
    {
        try
        {
            var response = await episodeSearchIndexerService.IndexEpisodes(
                plan.EpisodesToRepublish.Select(x => x.Id), c);
            if (response.IndexerState == IndexerState.Executed)
            {
                return true;
            }

            logger.LogError(
                "Search re-index for podcast '{podcastId}' did not succeed: indexer-state={indexerState}, request-state={requestState}.",
                plan.Podcast.Id, response.IndexerState, response.EpisodeIndexRequestState);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Search re-index failed for podcast '{podcastId}'.", plan.Podcast.Id);
        }

        return false;
    }

    private async Task<bool> RewriteShortUrls(PodcastRestorePlan plan)
    {
        try
        {
            var result = await shortnerService.Write(
                plan.EpisodesToRepublish.Select(e => new PodcastEpisode(plan.Podcast, e)));
            if (result.Success)
            {
                return true;
            }

            logger.LogError("Short-URL re-creation failed for podcast '{podcastId}'.", plan.Podcast.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Short-URL re-creation threw for podcast '{podcastId}'.", plan.Podcast.Id);
        }

        return false;
    }
}
