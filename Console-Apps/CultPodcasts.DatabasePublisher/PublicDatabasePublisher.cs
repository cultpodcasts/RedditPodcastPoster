using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using CultPodcasts.DatabasePublisher.Consistency;
using CultPodcasts.DatabasePublisher.Mapping;
using CultPodcasts.DatabasePublisher.PublicModels;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Writers;
using Spectre.Console;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher(
    ISafeFileEntityWriter safeFileEntityWriter,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PublicDatabasePublisher> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    /// <summary>
    /// Bounded concurrency for per-item child fetch + disk write.
    /// Parent FeedIterator iteration stays single-threaded (MoveNext is not safe concurrently).
    /// Child queries are independent per parent partition and may run in parallel.
    /// Publish order across items is not guaranteed; children within a file remain
    /// ordered by release descending.
    /// </summary>
    private static readonly int PublishParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

    public async Task Run()
    {
        AnsiConsole.MarkupLine(
            $"[grey]Publish parallelism:[/] {PublishParallelism} (parent feed sequential; child fetch + write parallel)");

        await AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var fileKeyCheck = ctx.AddTask("File-key check: all kinds", maxValue: 1);
                var inventory = await ReadInventory();
                PublicFileKeyGuard.EnsureValid(inventory);
                fileKeyCheck.Increment(1);
                fileKeyCheck.StopTask();

                await PublishKindAsync(
                    ctx,
                    "Podcasts",
                    podcastRepository.Count(),
                    podcastRepository.GetAllBy(p => !(p.Removed ?? false)),
                    PublishPodcastAsync);
                await PublishKindAsync(
                    ctx,
                    "TV shows",
                    tvShowRepository.Count(),
                    tvShowRepository.GetAllBy(s => !(s.Removed ?? false)),
                    PublishTvShowAsync);
                await PublishKindAsync(
                    ctx,
                    "News organisations",
                    newsOrganisationRepository.Count(),
                    newsOrganisationRepository.GetAllBy(o => !(o.Removed ?? false)),
                    PublishNewsOrganisationAsync);
                await PublishKindAsync(
                    ctx,
                    "Films",
                    filmRepository.Count(),
                    filmRepository.GetAllBy(f => !(f.Removed ?? false)),
                    PublishFilmAsync);
            });

        AnsiConsole.MarkupLine("[green]Finished publishing public database.[/]");
    }

    private async Task<IReadOnlyCollection<CatalogueItem>> ReadInventory()
    {
        var inventory = new List<CatalogueItem>();
        await foreach (var podcast in podcastRepository.GetAll())
        {
            inventory.Add(new CatalogueItem(podcast.Id, podcast.FileKey, PublicKind.Podcast, podcast.Removed ?? false));
        }

        await foreach (var show in tvShowRepository.GetAll())
        {
            inventory.Add(new CatalogueItem(show.Id, show.FileKey, PublicKind.TvShow, show.Removed ?? false));
        }

        await foreach (var organisation in newsOrganisationRepository.GetAll())
        {
            inventory.Add(new CatalogueItem(
                organisation.Id,
                organisation.FileKey,
                PublicKind.NewsOrganisation,
                organisation.Removed ?? false));
        }

        await foreach (var film in filmRepository.GetAll())
        {
            inventory.Add(new CatalogueItem(film.Id, film.FileKey, PublicKind.Film, film.Removed ?? false));
        }

        return inventory;
    }

    private static async Task PublishKindAsync<T>(
        ProgressContext ctx,
        string description,
        Task<int> countTask,
        IAsyncEnumerable<T> source,
        Func<T, Task> publishAsync)
    {
        var count = await countTask;
        var progress = ctx.AddTask(description, maxValue: Math.Max(count, 1));
        if (count == 0)
        {
            progress.Increment(1);
            progress.StopTask();
            return;
        }

        await PublishAllParallelAsync(source, publishAsync, progress);
        progress.StopTask();
    }

    private async Task PublishPodcastAsync(Podcast podcast)
    {
        var episodeCount = await episodeRepository.Count(podcast.Id);
        if (episodeCount <= 0)
        {
            return;
        }

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
        await safeFileEntityWriter.Write(publicPodcast);
    }

    private async Task PublishTvShowAsync(TvShow show)
    {
        var episodeCount = await tvShowEpisodeRepository.Count(show.Id);
        if (episodeCount <= 0)
        {
            return;
        }

        var episodes = await tvShowEpisodeRepository.GetByTvShowId(show.Id, e => !e.Removed).ToListAsync();
        await safeFileEntityWriter.Write(PublicCatalogueMapper.ToPublic(show, episodes));
    }

    private async Task PublishNewsOrganisationAsync(NewsOrganisation organisation)
    {
        var reportCount = await newsReportRepository.Count(organisation.Id);
        if (reportCount <= 0)
        {
            return;
        }

        var reports = await newsReportRepository.GetByNewsOrganisationId(organisation.Id, r => !r.Removed)
            .ToListAsync();
        await safeFileEntityWriter.Write(PublicCatalogueMapper.ToPublic(organisation, reports));
    }

    private Task PublishFilmAsync(Film film) =>
        safeFileEntityWriter.Write(PublicCatalogueMapper.ToPublic(film));

    /// <summary>
    /// Single Cosmos reader + N publishers via a bounded channel. Avoids concurrent
    /// FeedIterator.MoveNext on the parent query while overlapping child fetch/write
    /// with the next parent page fetch.
    /// </summary>
    private static async Task PublishAllParallelAsync<T>(
        IAsyncEnumerable<T> source,
        Func<T, Task> publishAsync,
        ProgressTask progress)
    {
        var channel = Channel.CreateBounded<T>(new BoundedChannelOptions(PublishParallelism * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var workers = Enumerable.Range(0, PublishParallelism).Select(async _ =>
        {
            await foreach (var item in channel.Reader.ReadAllAsync())
            {
                await publishAsync(item);
                progress.Increment(1);
            }
        }).ToArray();

        try
        {
            await foreach (var item in source)
            {
                await channel.Writer.WriteAsync(item);
            }

            channel.Writer.Complete();
            await Task.WhenAll(workers);
        }
        catch (Exception)
        {
            channel.Writer.TryComplete();
            try
            {
                await Task.WhenAll(workers);
            }
            catch
            {
                // Prefer the producer/publish failure that started the shutdown.
            }

            throw;
        }
    }
}
