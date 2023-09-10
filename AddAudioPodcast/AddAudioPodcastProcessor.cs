using System.Text.RegularExpressions;
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
    private readonly IApplePodcastEnricher _applePodcastEnricher;
    private readonly IndexOptions _indexOptions = new(null, true);
    private readonly ILogger<AddAudioPodcastProcessor> _logger;
    private readonly RedditPodcastPoster.Common.Podcasts.PodcastFactory _podcastFactory;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;
    private readonly ISpotifyClient _spotifyClient;

    public AddAudioPodcastProcessor(
        IPodcastRepository podcastRepository,
        ISpotifyClient spotifyClient,
        RedditPodcastPoster.Common.Podcasts.PodcastFactory podcastFactory,
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

            await _podcastUpdater.Update(podcast, _indexOptions);

            if (!string.IsNullOrWhiteSpace(request.EpisodeTitleRegex))
            {
                var includeEpisodesRegex = new Regex(request.EpisodeTitleRegex, RegexOptions.Compiled|RegexOptions.IgnoreCase);

//                var prototypeRegex =
//                    new Regex(
//                        @"\b(?:cult|cults|culty|Jonestown|MLM|Beachbody|Scientology|Marcus Wesson|Mormonism|Greek Life|Leslie Van Houten|OneTaste|Jesus Camp)\b",
////                        @"((\bCult?\b)?(\bCults?\b)?(\bCulty?\b)?(Jonestown)?(MLM)?(Beachbody)?(Scientology)?(Marcus Wesson)?(Mormonism)?(Greek Life)?(Leslie Van Houten)?(OneTaste)?(Jesus Camp)?)*",
//                        RegexOptions.Compiled|RegexOptions.IgnoreCase);

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
                    else
                    {
                        var matchedTerm = match.Groups[0].Value;
                    }
                }

                foreach (var episode in episodesToRemove)
                {
                    podcast.Episodes.Remove(episode);
                }
            }

            await _podcastRepository.Save(podcast);
        }
        else
        {
            throw new ArgumentException($"Spotify-id '{request.SpotifyId}' already exists in podcast-repository");
        }
    }
}