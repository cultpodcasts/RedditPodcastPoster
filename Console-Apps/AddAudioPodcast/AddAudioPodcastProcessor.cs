using System.Text.RegularExpressions;
using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace AddAudioPodcast;

public class AddAudioPodcastProcessor
{
    private static readonly string Market = "GB";
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly IndexingContext _indexingContext = new(null, true);
    private readonly iTunesSearchManager _iTunesSearchManager;
    private readonly ILogger<AddAudioPodcastProcessor> _logger;
    private readonly PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifyPodcastEnricher _spotifyPodcastEnricher;

    public AddAudioPodcastProcessor(
        IPodcastRepository podcastRepository,
        ISpotifyClient spotifyClient,
        PodcastFactory podcastFactory,
        IApplePodcastEnricher applePodcastEnricher,
        ISpotifyPodcastEnricher spotifyPodcastEnricher,
        IPodcastUpdater podcastUpdater,
        iTunesSearchManager iTunesSearchManager,
        ILogger<AddAudioPodcastProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _spotifyClient = spotifyClient;
        _podcastFactory = podcastFactory;
        _applePodcastEnricher = applePodcastEnricher;
        _spotifyPodcastEnricher = spotifyPodcastEnricher;
        _podcastUpdater = podcastUpdater;
        _iTunesSearchManager = iTunesSearchManager;
        _logger = logger;
    }

    public async Task Create(AddAudioPodcastRequest request)
    {
        var indexingContext = new IndexingContext() {SkipPodcastDiscovery = false};
        var existingPodcasts = await _podcastRepository.GetAll().ToListAsync();
        Podcast? podcast = null;
        if (!request.AppleReleaseAuthority)
        {
            podcast = await GetSpotifyPodcast(request, indexingContext, existingPodcasts);
        }
        else
        {
            podcast = await GetApplePodcast(request, indexingContext, existingPodcasts);
        }

        if (podcast != null)
        {
            var result = await _podcastUpdater.Update(podcast, _indexingContext);

            if (!string.IsNullOrWhiteSpace(request.EpisodeTitleRegex))
            {
                var includeEpisodesRegex = new Regex(request.EpisodeTitleRegex,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

                podcast.IndexAllEpisodes = false;
                podcast.EpisodeIncludeTitleRegex = request.EpisodeTitleRegex;
                List<Episode> episodesToRemove = new();
                foreach (var episode in podcast.Episodes)
                {
                    var match = includeEpisodesRegex.Match(episode.Title);
                    if (!match.Success)
                    {
                        episodesToRemove.Add(episode);
                    }
#if DEBUG
                    else
                    {
                        var matchedTerm = match.Groups[0].Value;
                    }
#endif
                }

                foreach (var episode in episodesToRemove)
                {
                    podcast.Episodes.Remove(episode);
                    _logger.LogInformation($"Removed episode '{episode.Title}' due to regex.");
                }

                if (episodesToRemove.Any())
                {
                    await _podcastRepository.Save(podcast);
                }
            }

            if (result.Success)
            {
                _logger.LogInformation(result.ToString());
            }
            else
            {
                _logger.LogError(result.ToString());
            }
        }
        else
        {
            var source = request.AppleReleaseAuthority ? "Apple" : "Spotify";
            _logger.LogError($"Could not find podcast with id '{request.PodcastId}' using '{source}' as source.");
        }
    }

    private async Task<Podcast> GetSpotifyPodcast(AddAudioPodcastRequest request, IndexingContext indexingContext,
        List<Podcast> existingPodcasts)
    {
        var spotifyPodcast = await _spotifyClient.Shows.Get(request.PodcastId, new ShowRequest {Market = Market});

        var podcast = existingPodcasts.SingleOrDefault(x => x.SpotifyId == request.PodcastId);
        if (podcast == null)
        {
            podcast = _podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();

            await _applePodcastEnricher.AddId(podcast);
        }

        return podcast;
    }


    private async Task<Podcast?> GetApplePodcast(AddAudioPodcastRequest request, IndexingContext indexingContext,
        List<Podcast> existingPodcasts)
    {
        var id = long.Parse(request.PodcastId);
        var applePodcast = (await _iTunesSearchManager.GetPodcastById(id)).Podcasts.SingleOrDefault();

        if (applePodcast == null)
        {
            _logger.LogError($"No apple-podcast found for apple-id '{id}'.");
            return null;
        }

        var podcast = existingPodcasts.SingleOrDefault(x => x.AppleId == id);
        if (podcast == null)
        {
            podcast = _podcastFactory.Create(applePodcast.Name);
            podcast.AppleId = applePodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = applePodcast.ArtistName.Trim();
            podcast.ReleaseAuthority = Service.Apple;

            await _spotifyPodcastEnricher.AddIdAndUrls(podcast, indexingContext);
        }

        return podcast;
    }
}