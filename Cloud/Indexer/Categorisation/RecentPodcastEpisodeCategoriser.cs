﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.AI;
using RedditPodcastPoster.Persistence.Abstractions;

namespace Indexer.Categorisation;

public class RecentPodcastEpisodeCategoriser(
    IPodcastRepository podcastRepository,
    ICategoriser categoriser,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task Categorise()
    {
        var since = DateTime.UtcNow.AddDays(-7);

        var podcasts =
            await podcastRepository.GetAllBy(x => x.Episodes.Any(y => y.Release > since && !y.Subjects.Any()));
        foreach (var podcast in podcasts)
        {
            var updated = false;
            foreach (var episode in podcast.Episodes.Where(x => x.Release > since && !x.Subjects.Any()))
            {
                var updatedEpisode = await categoriser.Categorise(
                    episode,
                    podcast.IgnoredAssociatedSubjects,
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