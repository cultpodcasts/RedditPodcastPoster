using CultPodcasts.DatabasePublisher.PublicModels;
using Konsole;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher(
    IFileRepository fileRepository,
    ICosmosDbRepository cosmosDbRepository,
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<CosmosDbRepository> logger
#pragma warning restore CS9113 // Parameter is unread.
)
{
    public async Task Run()
    {
        var podcastIds = await cosmosDbRepository.GetAllIds<Podcast>().ToArrayAsync();

        var progress = new ProgressBar(podcastIds.Length);
        var ctr = 0;
        foreach (var podcastId in podcastIds)
        {
            var podcast = await cosmosDbRepository.Read<Podcast>(podcastId.ToString());
            progress.Refresh(ctr, $"Downloaded {podcast.FileKey}");

            if (podcast != null && !podcast.IsRemoved() && podcast.Episodes.Any(x => x is {Removed: false}))
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
                publicPodcast.Episodes = podcast.Episodes
                    .Where(x => x is {Removed: false})
                    .Select(oldEpisode => new PublicEpisode
                    {
                        Id = oldEpisode.Id,
                        AppleId = oldEpisode.AppleId,
                        Description = string.IsNullOrWhiteSpace(oldEpisode.Description) ? null : oldEpisode.Description,
                        Explicit = oldEpisode.Explicit,
                        Length = oldEpisode.Length,
                        Release = oldEpisode.Release,
                        SpotifyId = string.IsNullOrWhiteSpace(oldEpisode.SpotifyId) ? null : oldEpisode.SpotifyId,
                        Title = oldEpisode.Title,
                        YouTubeId = string.IsNullOrWhiteSpace(oldEpisode.YouTubeId) ? null : oldEpisode.YouTubeId,
                        Urls = new PublicServiceUrls
                        {
                            Apple = oldEpisode.Urls.Apple,
                            Spotify = oldEpisode.Urls.Spotify,
                            YouTube = oldEpisode.Urls.YouTube
                        },
                        Subjects = oldEpisode.Subjects.Any() ? oldEpisode.Subjects : null
                    })
                    .OrderByDescending(x => x.Release)
                    .ToList();

                await fileRepository.Write(publicPodcast);
            }

            if (++ctr == podcastIds.Length)
            {
                progress.Refresh(ctr, "Finished");
            }
        }
    }
}