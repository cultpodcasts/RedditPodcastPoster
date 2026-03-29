using CultPodcasts.DatabasePublisher.PublicModels;
using Konsole;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisherV2(
    ISafeFileEntityWriter safeFileEntityWriter,
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PublicDatabasePublisherV2> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run()
    {
        var init = new ProgressBar(1);
        var c = 0;
        init.Refresh(c, "Testing Podcast File-keys");
        await AreUnique(podcastRepository.GetAll().Select(p => p.FileKey), "Podcasts");
        init.Refresh(++c, "Tested Podcast File-keys");


        var podcastCount = await podcastRepository.Count();
        var podcasts = podcastRepository.GetAllBy(p => !(p.Removed ?? false));
        var progress = new ProgressBar(podcastCount);
        var ctr = 0;

        await foreach (var podcast in podcasts)
        {
            progress.Refresh(ctr, $"Processing {podcast.FileKey}");
            var episodeCount = await episodeRepository.Count(podcast.Id);

            if (episodeCount > 0)
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

                await foreach (var episode in episodes)
                {
                    var publicEpisode = new PublicEpisode
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
                    };
                    publicEpisodes.Add(publicEpisode);
                }

                publicPodcast.Episodes = publicEpisodes.OrderByDescending(x => x.Release).ToList();
                await safeFileEntityWriter.Write(publicPodcast);
            }

            if (++ctr == podcastCount)
            {
                progress.Refresh(ctr, "Finished");
            }
        }

        Console.WriteLine();
    }

    private static async Task AreUnique(IAsyncEnumerable<string> allFileKeys, string name)
    {
        var distinct = new HashSet<string>();
        var duplicate = new HashSet<string>();
        await foreach (var fileKey in allFileKeys)
        {
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