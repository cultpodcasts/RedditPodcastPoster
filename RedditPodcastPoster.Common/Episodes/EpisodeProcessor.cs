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
    private readonly IPodcastEpisodePoster _podcastEpisodePoster;

    public EpisodeProcessor(
        IPodcastRepository podcastRepository,
        IEpisodeResolver episodeResolver,
        IPodcastEpisodePoster podcastEpisodePoster,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<EpisodeProcessor> logger)
    {
        _podcastRepository = podcastRepository;
        _episodeResolver = episodeResolver;
        _podcastEpisodePoster = podcastEpisodePoster;
        _postingCriteria = postingCriteria.Value;
        _logger = logger;
    }

    public async Task<ProcessResponse> PostEpisodesSinceReleaseDate(DateTime since)
    {
        _logger.LogInformation($"{nameof(PostEpisodesSinceReleaseDate)} Finding episodes released since '{since}'.");
        var matchingPodcastEpisodes =
            await _episodeResolver.ResolveSinceReleaseDate(since);
        if (!matchingPodcastEpisodes.Any())
        {
            return ProcessResponse.Successful(
                $"Could not find episodes released since {since.ToString(CultureInfo.InvariantCulture)}");
        }

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes.OrderByDescending(x => x.Episode.Release))
        {
            if (!matchingPodcastEpisode.Episode.Posted)
            {
                if (matchingPodcastEpisode.Episode.Length >= _postingCriteria.MinimumDuration)
                {
                    var result = await _podcastEpisodePoster.PostPodcastEpisode(matchingPodcastEpisode);
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
        var messages = new List<string>();
        var failures = false;
        if (matchingPodcastEpisodeResults.Any(x => !x.Success))
        {
            failures = true;
            messages.Add("Failures:");
            messages.AddRange(matchingPodcastEpisodeResults.Where(x => !x.Success).Select(x => x.Message));
        }

        if (matchingPodcastEpisodeResults.Any(x => x.Success))
        {
            messages.Add("Success:");
            messages.AddRange(matchingPodcastEpisodeResults.Select(x => x.Message));
        }

        var result = string.Join(", ", messages);
        return failures ? ProcessResponse.Fail(result) : ProcessResponse.Successful(result);
    }

    public async Task<ProcessResponse?> PostEpisodeFromSpotifyUrl(Uri spotifyUri)
    {
        var matchingPodcastEpisode = await _episodeResolver.ResolveServiceUrl(spotifyUri);
        if (matchingPodcastEpisode.Podcast == null || matchingPodcastEpisode.Episode == null)
        {
            return ProcessResponse.Fail($"Could not find spotify-url {spotifyUri}");
        }

        return await _podcastEpisodePoster.PostPodcastEpisode(matchingPodcastEpisode);
    }
}