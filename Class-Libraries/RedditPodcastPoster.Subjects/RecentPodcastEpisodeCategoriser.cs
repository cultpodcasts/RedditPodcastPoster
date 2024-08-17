using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class RecentPodcastEpisodeCategoriser(
    IPodcastRepository podcastRepository,
    ICategoriser categoriser,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(7);

        var podcasts =
            await podcastRepository.GetAllBy(x =>
                    (!x.Removed.IsDefined() || x.Removed == false) &&
                    x.Episodes.Any(y => y.Release > since && !y.Subjects.Any()), x => new {guid = x.Id, x.Name})
                .ToListAsync();
        logger.LogInformation(
            $"Categorising podcasts: {string.Join(", ", podcasts.Select(x => $"'{x.Name}' ({x.guid})"))}");

        foreach (var podcastDetails in podcasts)
        {
            logger.LogInformation($"Categorise podcast '{podcastDetails.Name}'.");
            var updated = false;
            var podcast = await podcastRepository.GetPodcast(podcastDetails.guid);
            foreach (var episode in podcast.Episodes.Where(x => x.Release > since && !x.Subjects.Any()))
            {
                logger.LogInformation($"Categorise episode '{episode.Title}'.");
                var updatedEpisode = await categoriser.Categorise(
                    episode,
                    podcast.IgnoredAssociatedSubjects,
                    podcast.IgnoredSubjects,
                    podcast.DefaultSubject);

                if (updatedEpisode)
                {
                    logger.LogInformation(
                        $"{nameof(RecentPodcastEpisodeCategoriser)}: Podcast '{podcast.Name}' with id '{podcast.Id}' and episode with id {episode.Id}, updated subjects: '{string.Join(",", episode.Subjects.Select(x => $"'{x}'"))}'.");
                }

                updated |= updatedEpisode;
            }

            if (updated)
            {
                await podcastRepository.Save(podcast);
            }
        }
    }
}