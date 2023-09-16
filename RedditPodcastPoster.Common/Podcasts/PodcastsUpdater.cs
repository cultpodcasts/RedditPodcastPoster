using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.EliminationTerms;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.Podcasts;

public class PodcastsUpdater : IPodcastsUpdater
{
    private readonly IEliminationTermsRepository _eliminationTermsRepository;
    private readonly ILogger<PodcastsUpdater> _logger;
    private readonly IPodcastFilter _podcastFilter;
    private readonly IPodcastRepository _podcastRepository;
    private readonly IPodcastUpdater _podcastUpdater;

    public PodcastsUpdater(
        IPodcastUpdater podcastUpdater,
        IPodcastRepository podcastRepository,
        IPodcastFilter podcastFilter,
        IEliminationTermsRepository eliminationTermsRepository,
        ILogger<PodcastsUpdater> logger
    )
    {
        _podcastUpdater = podcastUpdater;
        _podcastRepository = podcastRepository;
        _podcastFilter = podcastFilter;
        _eliminationTermsRepository = eliminationTermsRepository;
        _logger = logger;
    }

    public async Task UpdatePodcasts(IndexOptions indexOptions)
    {
        _logger.LogInformation(
            indexOptions.ReleasedSince.HasValue
                ? $"{nameof(UpdatePodcasts)} Indexing with options released-since: '{indexOptions.ReleasedSince:dd/MM/yyyy HH:mm:ss}', bypass-youtube: '{indexOptions.SkipYouTubeUrlResolving}'."
                : $"{nameof(UpdatePodcasts)} Indexing with options released-since: Null, bypass-youtube: '{indexOptions.SkipYouTubeUrlResolving}'.");

        IEnumerable<Podcast> podcasts = await _podcastRepository.GetAll().ToListAsync();
        var eliminationTerms = await _eliminationTermsRepository.Get();
        foreach (var podcast in podcasts)
        {
            if (podcast.IndexAllEpisodes || !string.IsNullOrWhiteSpace(podcast.EpisodeIncludeTitleRegex))
            {
                _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing '{podcast.Name}' ({podcast.Id}).");
                await _podcastUpdater.Update(podcast, indexOptions);
            }

            _podcastFilter.Filter(podcast, eliminationTerms.Terms);
            await _podcastRepository.Update(podcast);
        }

        _logger.LogInformation($"{nameof(UpdatePodcasts)} Indexing complete.");
    }
}