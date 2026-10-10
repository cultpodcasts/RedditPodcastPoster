using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;

namespace RemoveEpisodes.PodcastRestore;

/// <summary>
///     Undo of an accidental podcast removal. Dry run by default; <c>--non-dry-run</c> persists.
/// </summary>
public class RestorePodcastProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    IShortnerService shortnerService,
    ILogger<RestorePodcastProcessor> logger)
{
    public async Task<int> Process(RestorePodcastRequest request, CancellationToken c = default)
    {
        var ids = request.PodcastIds.ToList();
        var names = request.PodcastNames.ToList();
        if (ids.Count == 0 && names.Count == 0)
        {
            logger.LogError("Supply at least one --podcast-id or --podcast-name.");
            return 2;
        }

        var podcasts = await ResolvePodcasts(ids, names);
        if (podcasts == null)
        {
            return 2;
        }

        var mode = request.IsNonDryRun ? "APPLY" : "DRY RUN";
        var exit = 0;
        foreach (var podcast in podcasts)
        {
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

    private async Task<List<Podcast>?> ResolvePodcasts(List<Guid> ids, List<string> names)
    {
        var resolved = new Dictionary<Guid, Podcast>();
        foreach (var id in ids)
        {
            var podcast = await podcastRepository.GetPodcast(id);
            if (podcast == null)
            {
                logger.LogError("No podcast with id '{podcastId}'.", id);
                return null;
            }

            resolved[podcast.Id] = podcast;
        }

        foreach (var name in names)
        {
            var trimmed = name.Trim();
            var matches = await podcastRepository.GetAllBy(x => x.Name == trimmed).ToListAsync();
            if (matches.Count != 1)
            {
                logger.LogError(
                    "Expected exactly one podcast named '{name}', found {count}{ids}. Use --podcast-id instead.",
                    trimmed, matches.Count,
                    matches.Count > 0 ? ": " + string.Join(", ", matches.Select(x => $"'{x.Id}'")) : string.Empty);
                return null;
            }

            resolved[matches[0].Id] = matches[0];
        }

        return resolved.Values.ToList();
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
        plan.ApplyToModels();
        if (plan.PodcastNeedsUnremove)
        {
            await podcastRepository.Save(plan.Podcast);
        }

        foreach (var episode in plan.EpisodesToClearParentRemoved)
        {
            await episodeRepository.Save(episode);
        }

        var ok = true;
        if (plan.EpisodesToRepublish.Count > 0)
        {
            try
            {
                var response = await episodeSearchIndexerService.IndexEpisodes(
                    plan.EpisodesToRepublish.Select(x => x.Id), c);
                logger.LogInformation("Search re-index for '{podcastId}': {state}.", plan.Podcast.Id,
                    response.EpisodeIndexRequestState);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Search re-index failed for podcast '{podcastId}'. Cosmos is restored; re-run to retry.",
                    plan.Podcast.Id);
                ok = false;
            }

            if (!skipShortner)
            {
                var result = await shortnerService.Write(
                    plan.EpisodesToRepublish.Select(e => new PodcastEpisode(plan.Podcast, e)));
                if (!result.Success)
                {
                    logger.LogError("Short-URL re-creation failed for podcast '{podcastId}'. Re-run to retry.",
                        plan.Podcast.Id);
                    ok = false;
                }
            }
        }

        logger.LogInformation("[APPLY] Restored podcast '{name}' ({id}).", plan.Podcast.Name, plan.Podcast.Id);
        return ok;
    }
}
