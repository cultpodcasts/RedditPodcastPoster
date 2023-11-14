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
            .Where(x => x.Episodes.Any(y => y.Release > since && !y.Subjects.Any())).ToListAsync();
        foreach (var podcast in podcasts)
        {
            var updated = false;
            foreach (var episode in podcast.Episodes.Where(x =>
                         x.Release > since && !x.Subjects.Any()))
            {
                var originalSubjects = episode.Subjects.ToArray();
                var updatedEpisode = await _categoriser.Categorise(episode, podcast.IgnoredAssociatedSubjects);

                if (updatedEpisode)
                {
                    _logger.LogInformation(
                        $"{nameof(RecentPodcastEpisodeCategoriser)}: Podcast '{podcast.Name}' with id '{podcast.Id}' and episode with id {episode.Id}, updated subjects: '{string.Join(",", episode.Subjects.Select(x => $"'{x}'"))}'.");
                }

                updated |= updatedEpisode;
            }

            if (updated)
            {
                await _podcastRepository.Save(podcast);
            }
        }
    }
}