using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CultPodcasts.DatabasePublisher.Consistency;
using CultPodcasts.DatabasePublisher.Mapping;
using CultPodcasts.DatabasePublisher.PublicModels;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Spectre.Console;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher(
    PublicationFolder folder,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
    ILogger<PublicDatabasePublisher> logger
)
{
    /// <summary>
    /// Bounded concurrency for the per-item child count and fetch. Each parent's children are an independent
    /// query on its own partition, so they may run in parallel; the parents are read once, up front.
    /// Files are written in an unspecified order; children within a file are ordered newest first.
    /// </summary>
    private static readonly int PublishParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

    /// <summary>
    /// Works out, then does, the publish of the folder this run is in. Phase one touches nothing: it reads the
    /// files already there as the baseline, reads every item once, checks the file keys, counts each live
    /// parent's children and plans every write, move and deletion. Phase two applies the plan: deletions, then
    /// moves (a file whose item moved to another file name is renamed first, so git sees the history follow),
    /// then the writes, each of which may only create or only replace as planned. When the plan has a file
    /// that would disappear unexplained the plan is returned unapplied. Throws
    /// <see cref="PublicationFolderException"/> when the folder is wrongly empty or populated for
    /// <see cref="PublishOptions.Initial"/>, and <see cref="PublicDatabaseConsistencyException"/> from the
    /// file-key check. Holds no state between runs; printing the report is the caller's job.
    /// </summary>
    public async Task<PublicationReport> Run(PublishOptions options)
    {
        var baseline = await folder.ReadBaselineAsync();
        if (baseline.Count == 0 && !options.Initial)
        {
            throw new PublicationFolderException(
                $"The output folder '{folder.FolderPath}' holds no published files. It must hold the previous publish " +
                "so that nothing can vanish unnoticed; run with --initial only for a genuine first publish.");
        }

        if (baseline.Count > 0 && options.Initial)
        {
            throw new PublicationFolderException(
                $"--initial was given but the output folder '{folder.FolderPath}' already holds {baseline.Count} published file(s).");
        }

        AnsiConsole.MarkupLine(
            $"[grey]Publish parallelism:[/] {PublishParallelism} (parents read once; child count and fetch parallel)");

        var (plan, candidates) = await CreateProgress().StartAsync(async ctx =>
        {
            var catalogue = await ReadCatalogueAsync(ctx.AddTask("Read catalogue", maxValue: 4));

            var fileKeyCheck = ctx.AddTask("File-key check: all kinds", maxValue: 1);
            PublicFileKeyGuard.EnsureValid(catalogue.Items);
            fileKeyCheck.Increment(1);
            fileKeyCheck.StopTask();

            var found = await FindCandidatesAsync(catalogue, ctx);
            var planned = PublicationPlanner.Plan(
                baseline,
                found.Select(c => c.Item).ToList(),
                catalogue.Items,
                options.Accepted);
            return (planned, found);
        });

        if (plan.IsBlocked)
        {
            return new PublicationReport(plan, Applied: false);
        }

        foreach (var removal in plan.Deletions)
        {
            folder.Delete(removal.FileKey);
            logger.LogInformation("Deleted '{FileKey}.json' ({Reason}, id {Id}).", removal.FileKey, removal.Reason, removal.Id);
        }

        foreach (var move in plan.Moves)
        {
            folder.Move(move.FileKey, move.MoveTo!);
            logger.LogInformation(
                "Renamed '{FromFileKey}.json' to '{ToFileKey}.json' ({Reason}, id {Id}).",
                move.FileKey,
                move.MoveTo,
                move.Reason,
                move.Id);
        }

        var candidatesByKey = candidates.ToDictionary(c => c.Item.FileKey, StringComparer.OrdinalIgnoreCase);
        await CreateProgress().StartAsync(async ctx =>
        {
            var progress = ctx.AddTask("Write files", maxValue: Math.Max(plan.Files.Count, 1));
            if (plan.Files.Count == 0)
            {
                progress.Increment(1);
            }

            await Parallel.ForEachAsync(
                plan.Files,
                new ParallelOptions { MaxDegreeOfParallelism = PublishParallelism },
                async (file, _) =>
                {
                    var data = await candidatesByKey[file.Item.FileKey].Build();
                    await folder.WriteAsync(data, file.Mode);
                    progress.Increment(1);
                });
            progress.StopTask();
        });

        return new PublicationReport(plan, Applied: true);
    }

    private static Progress CreateProgress() =>
        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn());

    private async Task<Catalogue> ReadCatalogueAsync(ProgressTask progress)
    {
        var podcasts = await podcastRepository.GetAll().ToListAsync();
        progress.Increment(1);
        var shows = await tvShowRepository.GetAll().ToListAsync();
        progress.Increment(1);
        var organisations = await newsOrganisationRepository.GetAll().ToListAsync();
        progress.Increment(1);
        var films = await filmRepository.GetAll().ToListAsync();
        progress.Increment(1);
        progress.StopTask();
        return new Catalogue(podcasts, shows, organisations, films);
    }

    /// <summary>
    /// The items that will have a file: live parents that have at least one child row (removed or not, as the
    /// podcast rule has always counted) and live films. Counting is the only child query here.
    /// </summary>
    private async Task<IReadOnlyList<Candidate>> FindCandidatesAsync(Catalogue catalogue, ProgressContext ctx)
    {
        var livePodcasts = catalogue.Podcasts.Where(p => !(p.Removed ?? false)).ToList();
        var liveShows = catalogue.TvShows.Where(s => !(s.Removed ?? false)).ToList();
        var liveOrganisations = catalogue.NewsOrganisations.Where(o => !(o.Removed ?? false)).ToList();
        var liveFilms = catalogue.Films.Where(f => !(f.Removed ?? false)).ToList();

        var parents = livePodcasts.Count + liveShows.Count + liveOrganisations.Count;
        var progress = ctx.AddTask("Count episodes and reports", maxValue: Math.Max(parents, 1));
        if (parents == 0)
        {
            progress.Increment(1);
        }

        var candidates = new ConcurrentBag<Candidate>();

        await CountEachAsync(livePodcasts, p => episodeRepository.Count(p.Id), p =>
            candidates.Add(Candidate.For(p.Id, p.FileKey, PublicKind.Podcast, () => BuildPodcastAsync(p))), progress);
        await CountEachAsync(liveShows, s => tvShowEpisodeRepository.Count(s.Id), s =>
            candidates.Add(Candidate.For(s.Id, s.FileKey, PublicKind.TvShow, () => BuildTvShowAsync(s))), progress);
        await CountEachAsync(liveOrganisations, o => newsReportRepository.Count(o.Id), o =>
            candidates.Add(Candidate.For(
                o.Id, o.FileKey, PublicKind.NewsOrganisation, () => BuildNewsOrganisationAsync(o))), progress);
        foreach (var film in liveFilms)
        {
            candidates.Add(Candidate.For(
                film.Id, film.FileKey, PublicKind.Film, () => Task.FromResult(PublicCatalogueMapper.ToPublic(film))));
        }

        progress.StopTask();
        return candidates.OrderBy(c => c.Item.FileKey, StringComparer.Ordinal).ToList();
    }

    private static Task CountEachAsync<T>(
        IReadOnlyList<T> parents,
        Func<T, Task<int>> countAsync,
        Action<T> hasChildren,
        ProgressTask progress) =>
        Parallel.ForEachAsync(
            parents,
            new ParallelOptions { MaxDegreeOfParallelism = PublishParallelism },
            async (parent, _) =>
            {
                if (await countAsync(parent) > 0)
                {
                    hasChildren(parent);
                }

                progress.Increment(1);
            });
    private async Task<PublicPodcast> BuildPodcastAsync(Podcast podcast)
    {
        var publicPodcast = new PublicPodcast(podcast.Id)
        {
            FileKey = podcast.FileKey,
            AppleId = podcast.AppleId,
            Name = podcast.Name,
            SpotifyId = string.IsNullOrWhiteSpace(podcast.SpotifyId) ? null : podcast.SpotifyId,
            YouTubeChannelId = string.IsNullOrWhiteSpace(podcast.YouTubeChannelId)
                ? null
                : podcast.YouTubeChannelId,
            YouTubePlaylistId = string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId)
                ? null
                : podcast.YouTubePlaylistId
        };

        var episodes = episodeRepository.GetByPodcastId(podcast.Id, e => !e.Removed);
        var publicEpisodes = new List<PublicEpisode>();

        // Episode FeedIterator for this podcast stays sequential (per-iterator MoveNext).
        await foreach (var episode in episodes)
        {
            publicEpisodes.Add(new PublicEpisode
            {
                Id = episode.Id,
                AppleId = EpisodeServicePresence.AppleEpisodeId(episode),
                Description = string.IsNullOrWhiteSpace(episode.Description) ? null : episode.Description,
                Explicit = episode.Explicit,
                Length = episode.Length,
                Release = episode.ReleaseUtc,
                SpotifyId = EpisodeServicePresence.SpotifyEpisodeId(episode),
                Title = episode.Title,
                YouTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode),
                Urls = new PublicServiceUrls
                {
                    Apple = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple),
                    Spotify = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify),
                    YouTube = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
                    BBC = EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcIplayer))
                          ?? EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds)),
                    InternetArchive = EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.InternetArchive))
                },
                Subjects = episode.Subjects.Any() ? episode.Subjects : null
            });
        }

        publicPodcast.Episodes = publicEpisodes.OrderByDescending(x => x.Release).ToList();
        return publicPodcast;
    }
    private async Task<PublicTvShow> BuildTvShowAsync(TvShow show)
    {
        var episodes = await tvShowEpisodeRepository.GetByTvShowId(show.Id, e => !e.Removed).ToListAsync();
        return PublicCatalogueMapper.ToPublic(show, episodes);
    }

    private async Task<PublicNewsOrganisation> BuildNewsOrganisationAsync(NewsOrganisation organisation)
    {
        var reports = await newsReportRepository.GetByNewsOrganisationId(organisation.Id, r => !r.Removed)
            .ToListAsync();
        return PublicCatalogueMapper.ToPublic(organisation, reports);
    }

    /// <summary>Everything in Cosmos, read once, whether or not it is live.</summary>
    private sealed record Catalogue(
        IReadOnlyList<Podcast> Podcasts,
        IReadOnlyList<TvShow> TvShows,
        IReadOnlyList<NewsOrganisation> NewsOrganisations,
        IReadOnlyList<Film> Films)
    {
        public IReadOnlyList<CatalogueItem> Items { get; } =
        [
            .. Podcasts.Select(p => new CatalogueItem(p.Id, p.FileKey, PublicKind.Podcast, p.Removed ?? false, p.Name)),
            .. TvShows.Select(s => new CatalogueItem(s.Id, s.FileKey, PublicKind.TvShow, s.Removed ?? false, s.Name)),
            .. NewsOrganisations.Select(o =>
                new CatalogueItem(o.Id, o.FileKey, PublicKind.NewsOrganisation, o.Removed ?? false, o.Name)),
            .. Films.Select(f => new CatalogueItem(f.Id, f.FileKey, PublicKind.Film, f.Removed ?? false, f.Name))
        ];
    }

    /// <summary>An item that will have a file, and how to build that file's content.</summary>
    private sealed record Candidate(PublishedItem Item, Func<Task<CosmosSelector>> Build)
    {
        public static Candidate For<T>(Guid id, string fileKey, PublicKind kind, Func<Task<T>> build)
            where T : CosmosSelector =>
            new(new PublishedItem(id, fileKey, kind), async () => await build());
    }
}
