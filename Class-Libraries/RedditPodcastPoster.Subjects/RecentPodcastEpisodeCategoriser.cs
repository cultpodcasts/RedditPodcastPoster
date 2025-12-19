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
    public async Task<IList<Guid>> Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(7);
        IList<Guid> updatedEpisodes = new List<Guid>();
        var podcasts =
            await podcastRepository.GetAllBy(x =>
                    (!x.Removed.IsDefined() || x.Removed == false) &&
                    x.Episodes.Any(y => y.Release > since && !y.Subjects.Any()), x => new { guid = x.Id, x.Name })
                .ToListAsync();
        logger.LogInformation(
            "Categorising podcasts: {podcastNamesAndGuids}", string.Join(", ", podcasts.Select(x => $"'{x.Name}' ({x.guid})")));

        foreach (var podcastDetails in podcasts)
        {
            logger.LogInformation("Categorise podcast '{Name}'.", podcastDetails.Name);
            var updated = false;
            var podcast = await podcastRepository.GetPodcast(podcastDetails.guid);
            foreach (var episode in podcast.Episodes.Where(x => x.Release > since && !x.Subjects.Any()))
            {
                logger.LogInformation("Categorise episode '{episodeTitle}'.", episode.Title);
                var updatedEpisode = await categoriser.Categorise(
                    episode,
                    podcast.IgnoredAssociatedSubjects,
                    podcast.IgnoredSubjects,
                    podcast.DefaultSubject,
                    podcast.DescriptionRegex);

                if (updatedEpisode)
                {
                    updatedEpisodes.Add(episode.Id);
                    logger.LogInformation(
                        "{class}: Podcast '{podcastName}' with id '{podcastId}' and episode with id {episodeId}, updated subjects: {subjects}.",
                        nameof(RecentPodcastEpisodeCategoriser), podcast.Name, podcast.Id, episode.Id,
                        string.Join(", ", episode.Subjects.Select(x => $"'{x}'")));
                }

                updated |= updatedEpisode;
            }

            if (updated)
            {
                await podcastRepository.Save(podcast);
            }
        }

        return updatedEpisodes;
    }
}