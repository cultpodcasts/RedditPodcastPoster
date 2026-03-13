using CultPodcasts.DatabasePublisher.PublicModels;
using Konsole;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;
using Podcast = RedditPodcastPoster.Models.V2.Podcast;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisherV2(
    ISafeFileEntityWriter safeFileEntityWriter,
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<PublicDatabasePublisherV2> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run()
    {
        var podcasts = await podcastRepository
            .GetAllBy(p => !(p.Removed ?? false))
            .ToListAsync();

        var multipleFileKeys = podcasts
            .GroupBy(x => x.FileKey)
            .Select(x => new { FileKey = x.Key, Count = x.Count() })
            .Where(x => x.Count > 1)
            .Select(x => x.FileKey)
            .ToArray();

        if (multipleFileKeys.Any())
        {
            throw new InvalidOperationException(
                $"Multiple File-keys exist: '{string.Join(", ", multipleFileKeys)}'.");
        }

        var progress = new ProgressBar(podcasts.Count);
        var ctr = 0;

        foreach (var podcast in podcasts)
        {
            progress.Refresh(ctr, $"Processing {podcast.FileKey}");

            var episodes = await episodeRepository
                .GetByPodcastId(podcast.Id)
                .Where(e => !e.Removed)
                .ToListAsync();

            if (episodes.Count > 0)
            {
                var publicPodcast = new PublicPodcast(podcast.Id)
                {
                    FileKey = podcast.FileKey,
                    AppleId = podcast.AppleId,
                    Name = podcast.Name,
                    SpotifyId = string.IsNullOrWhiteSpace(podcast.SpotifyId) ? null : podcast.SpotifyId,
                    YouTubeChannelId =
                        string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) ? null : podcast.YouTubeChannelId,
                    YouTubePlaylistId = string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId)
                        ? null
                        : podcast.YouTubePlaylistId
                };

                publicPodcast.Episodes = episodes
                    .Select(episode => new PublicEpisode
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
                    })
                    .OrderByDescending(x => x.Release)
                    .ToList();

                await safeFileEntityWriter.Write(publicPodcast);
            }

            if (++ctr == podcasts.Count)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }
}
