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

    public async Task<SpotifyEpisodeWrapper> FindEpisode(Podcast podcast, Episode episode)
    {
        SimpleEpisode? simpleEpisode = null;
        FullEpisode? fullEpisode = null;
        if (!string.IsNullOrWhiteSpace(episode.SpotifyId))
        {
            fullEpisode = await _spotifyClient.Episodes.Get(episode.SpotifyId);
        }

        if (fullEpisode == null)
        {
            Paging<SimpleEpisode>[] episodes;
            if (!string.IsNullOrWhiteSpace(podcast.SpotifyId))
            {
                var fullShow = await _spotifyClient.Shows.Get(podcast.SpotifyId,
                    new ShowRequest() {Market = SpotifyItemResolver.Market});
                episodes = new[] {fullShow.Episodes};
            }
            else
            {
                var podcasts = await _spotifyClient.Search.Item(
                    new SearchRequest(SearchRequest.Types.Show, podcast.Name)
                        {Market = Market});

                var podcastsEpisodes = podcasts.Shows.Items;
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(podcast, podcastsEpisodes);
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

            var matchingEpisode = _spotifySearcher.FindMatchingEpisode(episode, allEpisodes);
            simpleEpisode = matchingEpisode;
        }

        return new SpotifyEpisodeWrapper(fullEpisode, simpleEpisode);
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

            var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(podcast, podcasts.Shows.Items);
            if (podcast.Episodes.Any())
            {
                foreach (var candidatePodcast in matchingPodcasts)
                {
                    var pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id,
                        new ShowEpisodesRequest {Market = Market});

                    var allEpisodes = await _spotifyClient.PaginateAll(pagedEpisodes);

                    var matchingEpisode =
                        _spotifySearcher.FindMatchingEpisode(
                            podcast.Episodes.OrderByDescending(x => x.Release).First(),
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