using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Common.Episodes;

public class PodcastEpisodesPoster : IPodcastEpisodesPoster
{
    private readonly ILogger<PodcastEpisodesPoster> _logger;
    private readonly IPodcastEpisodePoster _podcastEpisodePoster;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastEpisodeFilter _podcastEpisodeFilter;
    private readonly PostingCriteria _postingCriteria;

    public PodcastEpisodesPoster(
        IPodcastRepository podcastRepository,
        IPodcastEpisodeFilter podcastEpisodeFilter,
        IPodcastEpisodePoster podcastEpisodePoster,
        IOptions<PostingCriteria> postingCriteria,
        ILogger<PodcastEpisodesPoster> logger)
    {
        _podcastRepository = podcastRepository;
        _podcastEpisodeFilter = podcastEpisodeFilter;
        _podcastEpisodePoster = podcastEpisodePoster;
        _postingCriteria = postingCriteria.Value;
        _logger = logger;
    }

    public async Task<IList<ProcessResponse>> PostNewEpisodes(
        DateTime since,
        IList<Podcast> podcasts)
    {
        var matchingPodcastEpisodes = _podcastEpisodeFilter.GetNewEpisodesReleasedSince(podcasts, since);
        if (!matchingPodcastEpisodes.Any())
        {
            return Array.Empty<ProcessResponse>();
        }

        var matchingPodcastEpisodeResults = new List<ProcessResponse>();
        foreach (var matchingPodcastEpisode in matchingPodcastEpisodes.OrderByDescending(x => x.Episode.Release))
        {
            if (matchingPodcastEpisode.Episode.Length >= _postingCriteria.MinimumDuration)
            {
                if (matchingPodcastEpisode.Episode.Urls.Spotify != null ||
                    matchingPodcastEpisode.Episode.Urls.YouTube != null)
                {
                    var result = await _podcastEpisodePoster.PostPodcastEpisode(matchingPodcastEpisode);
                    matchingPodcastEpisodeResults.Add(result);
                }
                else
                {
                    matchingPodcastEpisodeResults.Add(ProcessResponse.NoSuitableLink(
                        $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored as no Spotify or YouTube link."));
                }
            }
            else
            {
                matchingPodcastEpisode.Episode.Ignored = true;
                matchingPodcastEpisodeResults.Add(ProcessResponse.TooShort(
                    $"Episode with id {matchingPodcastEpisode.Episode.Id} and title '{matchingPodcastEpisode.Episode.Title}' from podcast '{matchingPodcastEpisode.Podcast.Name}' with podcast-id '{matchingPodcastEpisode.Podcast.Id}' was Ignored for being too short at '{matchingPodcastEpisode.Episode.Length}'."));
            }
        }

        foreach (var podcast in matchingPodcastEpisodes.Select(x => x.Podcast).Distinct())
        {
            await _podcastRepository.Save(podcast);
        }

        return matchingPodcastEpisodeResults;
    }
}