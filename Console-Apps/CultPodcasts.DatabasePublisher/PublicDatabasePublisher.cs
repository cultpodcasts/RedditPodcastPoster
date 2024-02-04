using CultPodcasts.DatabasePublisher.PublicModels;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using RedditPodcastPoster.Persistence.Abstractions;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher(
    IFileRepository fileRepository,
    ICosmosDbRepository cosmosDbRepository,
    ILogger<CosmosDbRepository> logger)
{
    public async Task Run()
    {
        var podcastIds = await cosmosDbRepository.GetAllIds<Podcast>().ToArrayAsync();

        foreach (var podcastId in podcastIds)
        {
            var podcast = await cosmosDbRepository.Read<Podcast>(podcastId.ToString());

            if (podcast != null && podcast.Episodes.Any(x => x is {Removed: false}))
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
        }
    }
}