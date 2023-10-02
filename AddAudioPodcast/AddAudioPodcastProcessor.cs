using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;

namespace AddAudioPodcast;

public class AddAudioPodcastProcessor
{
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly IndexingContext _indexingContext = new(null, true);
    private readonly ILogger<AddAudioPodcastProcessor> _logger;
    private readonly PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;
    private readonly ICachedSpotifyClient _spotifyClient;

    public AddAudioPodcastProcessor(
        IPodcastRepository podcastRepository,
        ICachedSpotifyClient spotifyClient,
        PodcastFactory podcastFactory,
        IApplePodcastEnricher applePodcastEnricher,
        IPodcastUpdater podcastUpdater,
        ILogger<AddAudioPodcastProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _spotifyClient = spotifyClient;
        _podcastFactory = podcastFactory;
        _applePodcastEnricher = applePodcastEnricher;
        _podcastUpdater = podcastUpdater;
        _logger = logger;
    }

    public async Task Create(AddAudioPodcastRequest request)
    {
        var indexingContext = new IndexingContext();
        var spotifyPodcast =
            await _spotifyClient.Shows.Get(request.SpotifyId, indexingContext);
        var existingPodcasts = await _podcastRepository.GetAll().ToListAsync();

        var podcast = existingPodcasts.SingleOrDefault(x => x.SpotifyId == request.SpotifyId);
        if (podcast == null)
        {
            podcast = _podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();

            await _applePodcastEnricher.AddId(podcast);
        }

        await _podcastUpdater.Update(podcast, _indexingContext);

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
            }
        }

        await _podcastRepository.Save(podcast);
    }
}