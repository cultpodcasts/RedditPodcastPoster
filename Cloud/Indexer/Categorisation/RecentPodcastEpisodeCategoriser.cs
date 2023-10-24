using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Indexer.Categorisation;

public class RecentPodcastEpisodeCategoriser : IRecentPodcastEpisodeCategoriser
{
    private readonly ICategoriser _categoriser;
    private readonly ILogger<RecentPodcastEpisodeCategoriser> _logger;
    private readonly IPodcastRepository _podcastRepository;

    public RecentPodcastEpisodeCategoriser(
        IPodcastRepository podcastRepository,
        ICategoriser categoriser,
        ILogger<RecentPodcastEpisodeCategoriser> logger)
    {
        _podcastRepository = podcastRepository;
        _categoriser = categoriser;
        _logger = logger;
    }

    public async Task Categorise()
    {
        var since = DateTime.UtcNow.AddDays(-7);
        var podcasts = await _podcastRepository.GetAll()
            .Where(x => x.Episodes.Any(y => y.Release > since && string.IsNullOrWhiteSpace(y.Subject))).ToListAsync();
        foreach (var podcast in podcasts)
        {
            var updated = false;
            foreach (var episode in podcast.Episodes.Where(x =>
                         x.Release > since && string.IsNullOrWhiteSpace(x.Subject)))
            {
                var originalSubject = episode.Subject;
                var originalCategory = episode.Category;
                var categorised = await _categoriser.Categorise(episode);

                if (episode.Subject != originalSubject)
                {
                    _logger.LogInformation(
                        $"Podcast '{podcast.Name}' with id '{podcast.Id}' and episode with id {episode.Id}, updated subject: '{episode.Subject}'.");
                }

                if (episode.Category != originalCategory)
                {
                    _logger.LogInformation(
                        $"Podcast '{podcast.Name}' with id '{podcast.Id}' and episode with id {episode.Id}, updated category: '{episode.Category}'.");
                }

                updated |= categorised;
            }

            if (updated)
            {
                await _podcastRepository.Save(podcast);
            }
        }
    }
}