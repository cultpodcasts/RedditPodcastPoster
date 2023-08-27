using CultPodcasts.DatabasePublisher.PublicModels;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Models;

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
        var podcasts = await _cosmosDbRepository.GetAll<Podcast>().ToListAsync();
        foreach (var podcast in podcasts)
        {
            var key = _fileRepository.KeySelector.GetKey(podcast);

            var publicPodcast = new PublicPodcast
            {
                Id = podcast.Id,
                AppleId = podcast.AppleId,
                Name = podcast.Name,
                SpotifyId = podcast.SpotifyId,
                YouTubeChannelId = podcast.YouTubeChannelId
            };
            publicPodcast.Episodes = podcast.Episodes.Select(oldEpisode => new PublicEpisode
            {
                Id = oldEpisode.Id,
                AppleId = oldEpisode.AppleId,
                Description = oldEpisode.Description,
                Explicit = oldEpisode.Explicit,
                Length = oldEpisode.Length,
                Release = oldEpisode.Release,
                SpotifyId = oldEpisode.SpotifyId,
                Title = oldEpisode.Title,
                YouTubeId = oldEpisode.YouTubeId,
                Urls = new PublicServiceUrls
                {
                    Apple = oldEpisode.Urls.Apple,
                    Spotify = oldEpisode.Urls.Spotify,
                    YouTube = oldEpisode.Urls.YouTube
                },
                Subjects = oldEpisode.Subjects
            }).ToList();

            await _fileRepository.Write(key, publicPodcast);
        }
    }
}