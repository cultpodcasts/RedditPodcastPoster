using CultPodcasts.DatabasePublisher.PublicModels;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace CultPodcasts.DatabasePublisher;

public class PublicDatabasePublisher
{
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<CosmosDbRepository> _logger;

    public PublicDatabasePublisher(IFileRepository fileRepository,
        ICosmosDbRepository cosmosDbRepository,
        ILogger<CosmosDbRepository> logger)
    {
        _fileRepository = fileRepository;
        _cosmosDbRepository = cosmosDbRepository;
        _logger = logger;
    }

    public async Task Run()
    {
        var partitionKey = Podcast.PartitionKey;
        var podcastIds =
            await _cosmosDbRepository.GetAllIds<Podcast>(partitionKey);
        foreach (var podcastId in podcastIds)
        {
            var podcast = await _cosmosDbRepository.Read<Podcast>(podcastId.ToString(), partitionKey);

            var publicPodcast = new PublicPodcast
            {
                Id = podcast!.Id,
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
                .Where(x => !x.Removed)
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

            await _fileRepository.Write(podcast.FileKey, publicPodcast);
        }
    }
}