using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace AddAudioPodcast;

public class PodcastFactory
{
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly ILogger<PodcastFactory> _logger;
    private readonly RedditPodcastPoster.Common.Podcasts.PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _podcastRepository;
    private readonly ISpotifyClient _spotifyClient;

    public PodcastFactory(
        IPodcastRepository podcastRepository,
        ISpotifyClient spotifyClient,
        RedditPodcastPoster.Common.Podcasts.PodcastFactory podcastFactory,
        IApplePodcastEnricher applePodcastEnricher,
        ILogger<PodcastFactory> logger)
    {
        _podcastRepository = podcastRepository;
        _spotifyClient = spotifyClient;
        _podcastFactory = podcastFactory;
        _applePodcastEnricher = applePodcastEnricher;
        _logger = logger;
    }

    public async Task Create(PodcastCreateRequest request)
    {
        var spotifyPodcast =
            await _spotifyClient.Shows.Get(request.SpotifyId, new ShowRequest {Market = SpotifyItemResolver.Market});
        var existingPodcasts = await _podcastRepository.GetAll().ToListAsync();
        if (existingPodcasts.All(x => x.SpotifyId != request.SpotifyId))
        {
            var podcast = _podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();
            
            await _applePodcastEnricher.AddId(podcast);
            await _podcastRepository.Save(podcast);
        }
        else
        {
            throw new ArgumentException($"Spotify-id '{request.SpotifyId}' already exists in podcast-repository");
        }
    }
}