using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using CultPodcasts.DatabasePublisher.PublicModels;
using Konsole;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Writers;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher(
    ISafeFileEntityWriter safeFileEntityWriter,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PublicDatabasePublisher> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    /// <summary>
    /// Bounded concurrency for per-podcast episode fetch + disk write.
    /// Podcast FeedIterator iteration stays single-threaded (MoveNext is not safe concurrently).
    /// Episode queries are independent per podcast partition and may run in parallel.
    /// Publish order across podcasts is not guaranteed; episodes within a podcast remain
    /// ordered by Release descending.
    /// </summary>
    private static readonly int PublishParallelism = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);

    public async Task Run()
    {
        var init = new ProgressBar(1);
        var c = 0;
        init.Refresh(c, "Testing Podcast File-keys");
        var allFileKeys = podcastRepository.GetAll().Select(p => (Id: p.Id, FileKey: p.FileKey));
        await AreUnique(allFileKeys, "Podcasts");
        init.Refresh(++c, "Tested Podcast File-keys");

        var podcastCount = await podcastRepository.Count();
        Console.WriteLine(
            $"Publish parallelism: {PublishParallelism} (podcast feed sequential; episode fetch + write parallel)");

        var progress = new ProgressBar(Math.Max(podcastCount, 1));
        var ctr = 0;
        var progressLock = new object();

        var podcasts = podcastRepository.GetAllBy(p => !(p.Removed ?? false));
        await PublishAllParallelAsync(
            podcasts,
            async podcast =>
            {
                await PublishPodcastAsync(podcast);
                var done = Interlocked.Increment(ref ctr);
                lock (progressLock)
                {
                    progress.Refresh(
                        done,
                        done >= podcastCount ? "Finished" : $"Processed {podcast.FileKey}");
                }
            });

        Console.WriteLine();
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
                AppleId = episode.AppleId,
                Description = string.IsNullOrWhiteSpace(episode.Description) ? null : episode.Description,
                Explicit = episode.Explicit,
                Length = episode.Length,
                Release = episode.Release,
                SpotifyId = string.IsNullOrWhiteSpace(episode.SpotifyId) ? null : episode.SpotifyId,
                Title = episode.Title,
                YouTubeId = string.IsNullOrWhiteSpace(episode.YouTubeId) ? null : episode.YouTubeId,
                Urls = new PublicServiceUrls
                {
                    Apple = episode.Urls.Apple,
                    Spotify = episode.Urls.Spotify,
                    YouTube = episode.Urls.YouTube,
                    BBC = episode.Urls.BBC,
                    InternetArchive = episode.Urls.InternetArchive
                },
                Subjects = episode.Subjects.Any() ? episode.Subjects : null
            });
        }

        publicPodcast.Episodes = publicEpisodes.OrderByDescending(x => x.Release).ToList();
        await safeFileEntityWriter.Write(publicPodcast);
    }

    /// <summary>
    /// Single Cosmose reader + N publishers via a bounded channel. Avoids concurrent
    /// FeedIterator.MoveNext on the podcast query while overlapping episode fetch/write
    /// with the next podcast page fetch.
    /// </summary>
    private static async Task PublishAllParallelAsync(
        IAsyncEnumerable<Podcast> source,
        Func<Podcast, Task> publishAsync)
    {
        var channel = Channel.CreateBounded<Podcast>(new BoundedChannelOptions(PublishParallelism * 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var workers = Enumerable.Range(0, PublishParallelism).Select(async _ =>
        {
            await foreach (var podcast in channel.Reader.ReadAllAsync())
            {
                await publishAsync(podcast);
            }
        }).ToArray();

        try
        {
            await foreach (var podcast in source)
            {
                await channel.Writer.WriteAsync(podcast);
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

    private static async Task AreUnique(IAsyncEnumerable<(Guid, string)> allFileKeys, string name)
    {
        var distinct = new HashSet<string>();
        var duplicate = new HashSet<string>();
        await foreach (var (id, fileKey) in allFileKeys)
        {
            if (string.IsNullOrWhiteSpace(fileKey))
            {
                throw new InvalidOperationException(
                    $"File-key for podcast-id {id} is null or whitespace in {name} container.");
            }

            if (!distinct.Add(fileKey))
            {
                duplicate.Add(fileKey);
            }
        }

        if (duplicate.Any())
        {
            throw new InvalidOperationException(
                $"Multiple File-keys exist in {name} container: '{string.Join(", ", duplicate)}'.");
        }
    }
}
