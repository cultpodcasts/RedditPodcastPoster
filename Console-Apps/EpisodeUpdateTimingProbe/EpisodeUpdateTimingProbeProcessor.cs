using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace EpisodeUpdateTimingProbe;

/// <summary>
/// Local timing probe for the expensive EpisodeUpdate tail steps (resolve, Azure Search index, homepage publish).
/// Does not Save episodes. IndexEpisode and PublishHomepage do perform their normal external writes.
/// </summary>
public class EpisodeUpdateTimingProbeProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    IHomepagePublisher homepagePublisher,
    ILogger<EpisodeUpdateTimingProbeProcessor> logger)
{
    public async Task<int> Run(EpisodeUpdateTimingProbeRequest request, CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "EpisodeUpdateTimingProbe — measures resolve / IndexEpisode / PublishHomepage (no episode Save).");
        Console.WriteLine(
            "Side effects: IndexEpisode writes Azure Search; PublishHomepage uploads R2 homepage JSON (and may refresh homepage cache).");
        Console.WriteLine();

        var total = Stopwatch.StartNew();

        var resolveSw = Stopwatch.StartNew();
        var (episode, podcast) = await ResolveAsync(request);
        resolveSw.Stop();

        if (episode == null || podcast == null)
        {
            Console.Error.WriteLine(
                $"Resolve failed: episode-id={request.EpisodeId}, podcast-id={request.PodcastId?.ToString() ?? "(none)"}.");
            return 2;
        }

        WriteLine("resolve", resolveSw.ElapsedMilliseconds,
            $"podcast='{podcast.Name}' episode-title='{episode.Title}' release={episode.Release:u}");

        long indexMs = -1;
        long homepageMs = -1;
        long parallelWallMs = -1;
        long parallelIndexMs = -1;
        long parallelHomepageMs = -1;

        if (!request.SkipIndex)
        {
            var indexSw = Stopwatch.StartNew();
            var indexResult = await episodeSearchIndexerService.IndexEpisode(podcast, episode, cancellationToken);
            indexSw.Stop();
            indexMs = indexSw.ElapsedMilliseconds;
            WriteLine("index", indexMs,
                $"indexer-state={indexResult.IndexerState} episode-state={indexResult.EpisodeIndexRequestState}");
        }
        else
        {
            Console.WriteLine("index: skipped");
        }

        if (!request.SkipHomepage)
        {
            var homepageSw = Stopwatch.StartNew();
            var homepageResult = await homepagePublisher.PublishHomepage();
            homepageSw.Stop();
            homepageMs = homepageSw.ElapsedMilliseconds;
            WriteLine("homepage", homepageMs, $"published={homepageResult.HomepagePublished}");
        }
        else
        {
            Console.WriteLine("homepage: skipped");
        }

        if (request.Parallel && !request.SkipIndex && !request.SkipHomepage)
        {
            Console.WriteLine();
            Console.WriteLine("Parallel pass (IndexEpisode + PublishHomepage via Task.WhenAll)...");

            var wall = Stopwatch.StartNew();
            var indexSw = new Stopwatch();
            var homepageSw = new Stopwatch();

            var indexTask = MeasureAsync(
                () => episodeSearchIndexerService.IndexEpisode(podcast, episode, cancellationToken),
                indexSw);
            var homepageTask = MeasureAsync(() => homepagePublisher.PublishHomepage(), homepageSw);

            await Task.WhenAll(indexTask, homepageTask);
            wall.Stop();

            parallelWallMs = wall.ElapsedMilliseconds;
            parallelIndexMs = indexSw.ElapsedMilliseconds;
            parallelHomepageMs = homepageSw.ElapsedMilliseconds;

            var indexResult = await indexTask;
            var homepageResult = await homepageTask;

            WriteLine("parallel-wall", parallelWallMs, null);
            WriteLine("parallel-index", parallelIndexMs,
                $"indexer-state={indexResult.IndexerState}");
            WriteLine("parallel-homepage", parallelHomepageMs,
                $"published={homepageResult.HomepagePublished}");
        }

        total.Stop();
        Console.WriteLine();
        Console.WriteLine(
            $"EpisodeUpdateTimingProbeSummary episode-id='{request.EpisodeId}' total-ms={total.ElapsedMilliseconds} resolve-ms={resolveSw.ElapsedMilliseconds} index-ms={FormatOptional(indexMs)} homepage-ms={FormatOptional(homepageMs)} parallel-wall-ms={FormatOptional(parallelWallMs)} parallel-index-ms={FormatOptional(parallelIndexMs)} parallel-homepage-ms={FormatOptional(parallelHomepageMs)}");

        logger.LogWarning(
            "EpisodeUpdateTimingProbeSummary episode-id='{EpisodeId}' total-ms={TotalMs} resolve-ms={ResolveMs} index-ms={IndexMs} homepage-ms={HomepageMs} parallel-wall-ms={ParallelWallMs} parallel-index-ms={ParallelIndexMs} parallel-homepage-ms={ParallelHomepageMs}",
            request.EpisodeId,
            total.ElapsedMilliseconds,
            resolveSw.ElapsedMilliseconds,
            indexMs,
            homepageMs,
            parallelWallMs,
            parallelIndexMs,
            parallelHomepageMs);

        return 0;
    }

    private async Task<(Episode? Episode, Podcast? Podcast)> ResolveAsync(EpisodeUpdateTimingProbeRequest request)
    {
        if (request.PodcastId is { } podcastId)
        {
            var episodeTask = episodeRepository.GetEpisode(podcastId, request.EpisodeId);
            var podcastTask = podcastRepository.GetPodcast(podcastId);
            await Task.WhenAll(episodeTask, podcastTask);
            return (episodeTask.Result, podcastTask.Result);
        }

        var episode = await episodeRepository.GetBy(x => x.Id == request.EpisodeId);
        if (episode == null)
        {
            return (null, null);
        }

        var podcast = await podcastRepository.GetPodcast(episode.PodcastId);
        return (episode, podcast);
    }

    private static void WriteLine(string step, long ms, string? detail)
    {
        Console.WriteLine(detail == null
            ? $"{step}: {ms} ms"
            : $"{step}: {ms} ms — {detail}");
    }

    private static string FormatOptional(long ms) => ms < 0 ? "n/a" : ms.ToString();

    private static async Task<T> MeasureAsync<T>(Func<Task<T>> work, Stopwatch stopwatch)
    {
        stopwatch.Restart();
        try
        {
            return await work();
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
