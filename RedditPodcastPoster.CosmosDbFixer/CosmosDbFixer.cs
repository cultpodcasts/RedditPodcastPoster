using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Persistence;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.CosmosDbDownloader;

public class CosmosDbFixer
{
    private readonly ICosmosDbRepository _cosmosDbRepository;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ILogger<CosmosDbRepository> _logger;

    public CosmosDbFixer(
        IPodcastRepository podcastRepository,
        ISpotifyClient spotifyClient,
        ILogger<CosmosDbRepository> logger)
    {
        _podcastRepository = podcastRepository;
        _spotifyClient = spotifyClient;
        _logger = logger;
    }

    public async Task Run()
    {
        var podcasts = await _podcastRepository.GetAll().ToListAsync();
        foreach (var podcast in podcasts)
        {
            var spotifyId = podcast.SpotifyId;
            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                var spotifyPodcast =
                    await _spotifyClient.Shows.Get(spotifyId, new ShowRequest { Market = SpotifyItemResolver.Market });
                podcast.Publisher = spotifyPodcast.Publisher.Trim();
            }

            podcast.IndexAllEpisodes = true;

            await _podcastRepository.Save(podcast);
        }
    }
}