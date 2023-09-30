using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Text;
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

    public async Task<FullEpisode?> FindEpisode(FindSpotifyEpisodeRequest request)
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
                    new ShowRequest {Market = Market});
                episodes = new[] {fullShow.Episodes};
            }
            else
            {
                var podcastSearchResponse = await _spotifyClient.Search.Item(
                    new SearchRequest(SearchRequest.Types.Show, request.PodcastName)
                        {Market = Market});

                var podcasts = podcastSearchResponse.Shows.Items;
                var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.PodcastName, podcasts);
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
                    new EpisodeRequest {Market = Market});
            }
        }

        return fullEpisode;
    }

    public async Task<SpotifyPodcastWrapper> FindPodcast(FindSpotifyPodcastRequest request)
    {
        SimpleShow? matchingSimpleShow = null;
        FullShow? matchingFullShow = null;
        if (!string.IsNullOrWhiteSpace(request.SpotifyId))
        {
            matchingFullShow = await _spotifyClient.Shows.Get(request.SpotifyId);
        }

        if (matchingFullShow == null)
        {
            var podcasts = await _spotifyClient.Search.Item(new SearchRequest(SearchRequest.Types.Show, request.Name)
                {Market = Market});

            var matchingPodcasts = _spotifySearcher.FindMatchingPodcasts(request.Name, podcasts.Shows.Items);
            if (request.Episodes.Any())
            {
                foreach (var candidatePodcast in matchingPodcasts)
                {
                    var pagedEpisodes = await _spotifyClient.Shows.GetEpisodes(candidatePodcast.Id,
                        new ShowEpisodesRequest {Market = Market});

                    var allEpisodes = await _spotifyClient.PaginateAll(pagedEpisodes);

                    var mostRecentEpisode = request.Episodes.OrderByDescending(x => x.Release).First();
                    var matchingEpisode =
                        _spotifySearcher.FindMatchingEpisode(
                            mostRecentEpisode.Title,
                            mostRecentEpisode.Release,
                            new[] {allEpisodes});
                    if (request.Episodes
                        .Select(x => x.Url?.ToString())
                        .Contains(matchingEpisode!.ExternalUrls.FirstOrDefault().Value))
                    {
                        matchingSimpleShow = candidatePodcast;
                        break;
                    }
                }
            }

            matchingSimpleShow = matchingPodcasts.MaxBy(x => Levenshtein.CalculateSimilarity(request.Name, x.Name));
        }

        return new SpotifyPodcastWrapper(matchingFullShow, matchingSimpleShow);
    }

    public async Task<IEnumerable<SimpleEpisode>> GetEpisodes(GetSpotifyPodcastEpisodesRequest request)
    {
        var pagedEpisodes =
            await _spotifyClient.Shows.GetEpisodes(request.SpotifyPodcastId, new ShowEpisodesRequest {Market = Market});
        var episodes = new List<SimpleEpisode>();
        try
        {
            if (request.ReleasedSince == null)
            {
                var fetch = await _spotifyClient.PaginateAll(pagedEpisodes);
                episodes = fetch.ToList();
            }
            else
            {
                episodes.AddRange(pagedEpisodes.Items);
                while (pagedEpisodes.Items.Last().GetReleaseDate() > request.ReleasedSince)
                {
                    await _spotifyClient.Paginate(pagedEpisodes).ToListAsync();
                }

                episodes = pagedEpisodes.Items;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failure to retrieve episodes from Spotify with spotify-id '{request.SpotifyPodcastId}'.");
            return Enumerable.Empty<SimpleEpisode>();
        }

        return episodes;
    }
}