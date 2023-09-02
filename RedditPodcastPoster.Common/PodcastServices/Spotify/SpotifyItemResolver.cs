using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
using RedditPodcastPoster.Models;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Common.PodcastServices.Spotify;

public class SpotifyItemResolver : ISpotifyItemResolver
{
    public const string Market = "GB";
    private readonly ILogger<SpotifyItemResolver> _logger;
    private readonly ISpotifyClient _spotifyClient;
    private readonly ISpotifySearcher _spotifySearcher;

    public SpotifyItemResolver(
        ISpotifyClient spotifyClient,
        ISpotifySearcher spotifySearcher,
        ILogger<SpotifyItemResolver> logger)
    {
        _spotifyClient = spotifyClient;
        _spotifySearcher = spotifySearcher;
        _logger = logger;
    }

    public async Task<SpotifyEpisodeWrapper> FindEpisode(FindSpotifyEpisodeRequest request)
    {
        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(request.EpisodeSpotifyId))
        {
            fullEpisode = await _spotifyClient.Episodes.Get(request.EpisodeSpotifyId);
        }

        if (fullEpisode == null)
        {
            Paging<SimpleEpisode>[] episodes;
            if (!string.IsNullOrWhiteSpace(request.PodcastSpotifyId))
            {
                var fullShow = await _spotifyClient.Shows.Get(request.PodcastSpotifyId,
                    new ShowRequest() {Market = SpotifyItemResolver.Market});
                episodes = new[] {fullShow.Episodes};
            }
            else
            {
                var podcasts = await _spotifyClient.Search.Item(
                    new SearchRequest(SearchRequest.Types.Show, request.PodcastName)
                        {Market = Market});

                var podcastsEpisodes = podcasts.Shows.Items;
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcastsEpisodes);
                var episodesFetches = matchingPodcasts.Select(async x =>
                    await _spotifyClient.Shows.GetEpisodes(x.Id,
                        new ShowEpisodesRequest {Market = Market}));
                episodes = await Task.WhenAll(episodesFetches);
            }

            IList<IList<SimpleEpisode>> allEpisodes = new List<IList<SimpleEpisode>>();
            foreach (var paging in episodes)
            {
                var simpleEpisodes = await _spotifyClient.PaginateAll(paging);
                allEpisodes.Add(simpleEpisodes);
            }

            var matchingEpisode =
                _spotifySearcher.FindMatchingEpisode(request.EpisodeTitle, request.Released, allEpisodes);
            if (matchingEpisode != null)
            {
                fullEpisode = await _spotifyClient.Episodes.Get(matchingEpisode.Id,
                    new EpisodeRequest() {Market = SpotifyItemResolver.Market});
            }
        }

        return new SpotifyEpisodeWrapper(fullEpisode);
    }

    public async Task<SpotifyPodcastWrapper> FindPodcast(Podcast podcast)
    {
        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
        {
            matchingFullShow = await _spotifyClient.Shows.Get(podcast.SpotifyId);
        }

        if (matchingFullShow == null)
        {
            var podcasts = await _spotifyClient.Search.Item(new SearchRequest(SearchRequest.Types.Show, podcast.Name)
                {Market = Market});

            var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(podcast.Name, podcasts.Shows.Items);
            if (podcast.Episodes.Any())
            {
                foreach (var candidatePodcast in matchingPodcasts)
                {
                    var pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id,
                        new ShowEpisodesRequest {Market = Market});

                    var allEpisodes = await _spotifyClient.PaginateAll(pagedEpisodes);

                    var mostRecentEpisode = podcast.Episodes.OrderByDescending(x => x.Release).First();
                    var matchingEpisode =
                        _spotifySearcher.FindMatchingEpisode(
                            mostRecentEpisode.Title,
                            mostRecentEpisode.Release,
                            new[] {allEpisodes});
                    if (podcast.Episodes.Select(x => x.Urls.Spotify!.ToString())
                        .Contains(matchingEpisode!.ExternalUrls.FirstOrDefault().Value))
                    {
                        matchingSimpleShow = candidatePodcast;
                        break;
                    }
                }
            }

            matchingSimpleShow = matchingPodcasts.MaxBy(x => Levenshtein.CalculateSimilarity(podcast.Name, x.Name));
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow);
    }

    public async Task<IEnumerable<SimpleEpisode>> GetEpisodes(Podcast podcast)
    {
        var episodes =
            await _spotifyClient.Shows.GetEpisodes(podcast.SpotifyId,
                new ShowEpisodesRequest {Market = Market});
        IEnumerable<SimpleEpisode> allEpisodes = await _spotifyClient.PaginateAll(episodes);

        return allEpisodes;
    }
}