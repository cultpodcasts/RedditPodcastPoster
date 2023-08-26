using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Common.PodcastServices;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common;

public class PodcastProcessor : IPodcastProcessor
{
    private readonly IEpisodeProcessor _episodeProcessor;
    private readonly IEpisodeProvider _episodeProvider;
    private readonly ILogger<PodcastProcessor> _logger;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastServicesEpisodeEnricher _podcastServicesEpisodeEnricher;

    public PodcastProcessor(
        IEpisodeProvider episodeProvider,
        IPodcastRepository podcastRepository,
        IPodcastServicesEpisodeEnricher podcastServicesEpisodeEnricher,
        IEpisodeProcessor episodeProcessor,
        ILogger<PodcastProcessor> logger)
    {
        _episodeProvider = episodeProvider;
        _podcastRepository = podcastRepository;
        _podcastServicesEpisodeEnricher = podcastServicesEpisodeEnricher;
        _episodeProcessor = episodeProcessor;
        _logger = logger;
    }

    public async Task<ProcessResponse> Process(ProcessRequest processRequest)
    {
        IEnumerable<Podcast> podcasts = await _podcastRepository.GetAll().ToListAsync();
        if (processRequest.RefreshEpisodes)
        {
            foreach (var podcast in podcasts)
            {
                var newEpisodes =
                    await _episodeProvider.GetEpisodes(podcast, processRequest.ReleasedSince,
                        processRequest.SkipYouTubeUrlResolving) ??
                    new List<Episode>();
                await _podcastRepository.Merge(podcast, newEpisodes, MergeEnrichedProperties);

                var episodes = podcast.Episodes;
                if (processRequest.ReleasedSince.HasValue)
                {
                    episodes = episodes.Where(x => x.Release > processRequest.ReleasedSince.Value).ToList();
                }

                await _podcastServicesEpisodeEnricher.EnrichEpisodes(podcast, episodes, processRequest.ReleasedSince,
                    processRequest.SkipYouTubeUrlResolving);
                await _podcastRepository.Update(podcast);
            }
        }

        if (processRequest.ReleasedSince != null)
        {
            return await _episodeProcessor.PostEpisodesSinceReleaseDate(processRequest.ReleasedSince.Value);
        }

        return ProcessResponse.Successful();
    }

    private void MergeEnrichedProperties(Episode existingEpisode, Episode episodeToMerge)
    {
        existingEpisode.Urls.Spotify ??= episodeToMerge.Urls.Spotify;
        existingEpisode.Urls.YouTube ??= episodeToMerge.Urls.YouTube;
        if (string.IsNullOrWhiteSpace(existingEpisode.SpotifyId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.SpotifyId))
        {
            existingEpisode.SpotifyId = episodeToMerge.SpotifyId;
        }

        if (string.IsNullOrWhiteSpace(existingEpisode.YouTubeId) &&
            !string.IsNullOrWhiteSpace(episodeToMerge.YouTubeId))
        {
            existingEpisode.YouTubeId = episodeToMerge.YouTubeId;
        }
    }
}