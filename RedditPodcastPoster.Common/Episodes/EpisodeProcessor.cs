using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Podcasts;

namespace RedditPodcastPoster.Common.Episodes;

public class EpisodeProcessor : IEpisodeProcessor
{
    private readonly IEpisodeResolver _episodeResolver;
    private readonly ILogger<EpisodeProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly PostingCriteria _postingCriteria;
    private readonly IResolvedPodcastEpisodePoster _resolvedPodcastEpisodePoster;

    public EpisodeProcessor(
        IPodcastRepository podcastRepository,
        IEpisodeResolver episodeResolver,
        IResolvedPodcastEpisodePoster resolvedPodcastEpisodePoster,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<EpisodeProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _episodeResolver = episodeResolver;
        _resolvedPodcastEpisodePoster = resolvedPodcastEpisodePoster;
        _postingCriteria = postingCriteria.Value;
        _logger = logger;
    }

    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(DateTime since)
    {
        var matchingPodcastEpisodes =
            await _episodeResolver.ResolveSinceReleaseDate(since);
        if (!matchingPodcastEpisodes.Any())
        {
            return ProcessResponse.Successful(
                $"Could not find episodes released since {since.ToString(CultureInfo.InvariantCulture)}");
        }

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes)
        {
            if (!matchingPodcastEpisode.Episode.Posted)
            {
                if (matchingPodcastEpisode.Episode.Length >= _postingCriteria.MinimumDuration)
                {
                    var result = await _resolvedPodcastEpisodePoster.PostResolvedPodcastEpisode(matchingPodcastEpisode);
                    matchingPodcastEpisodeResults.Add(result);
                }
                else
                {
                    matchingPodcastEpisode.Episode.Ignored = true;
                    matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
                }
            }
        }

        foreach (var podcast in matchingPodcastEpisodes.Select(x => x.Podcast).Distinct())
        {
            await _podcastRepository.Save(podcast);
        }

        return CreateResponse(matchingPodcastEpisodeResults);
    }

    private ProcessResponse CreateResponse(List<ProcessResponse> matchingPodcastEpisodeResults)
    {
        if (matchingPodcastEpisodeResults.Any(x => !x.Success))
        {
            var failureMessages = matchingPodcastEpisodeResults.Where(x => !x.Success).Select(x => x.Message);
            return ProcessResponse.Fail(string.Join(", ", failureMessages));
        }

        var successMessages = matchingPodcastEpisodeResults.Select(x => x.Message);
        return ProcessResponse.Successful(string.Join(", ", successMessages));
    }

    public async Task<ProcessResponse?> PostEpisodeFromSpotifyUrl(Uri spotifyUri)
    {
        var matchingPodcastEpisode = await _episodeResolver.ResolveServiceUrl(spotifyUri);
        if (matchingPodcastEpisode.Podcast == null || matchingPodcastEpisode.Episode == null)
        {
            return ProcessResponse.Fail($"Could not find spotify-url {spotifyUri}");
        }

        return await _resolvedPodcastEpisodePoster.PostResolvedPodcastEpisode(matchingPodcastEpisode);
    }
}