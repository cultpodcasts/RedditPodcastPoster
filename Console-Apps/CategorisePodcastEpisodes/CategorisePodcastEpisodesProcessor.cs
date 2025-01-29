using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesProcessor(
    IPodcastRepository repository,
    ISubjectEnricher subjectEnricher,
    IRecentPodcastEpisodeCategoriser recentEpisodeCategoriser,
    ILogger<CategorisePodcastEpisodesProcessor> logger)
{
    public async Task Run(CategorisePodcastEpisodesRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PodcastIds))
        {
            var podcastIds = request.PodcastIds.Split(",");
            foreach (var podcastId in podcastIds)
            {
                var podcast = await repository.GetPodcast(Guid.Parse(podcastId));
                if (podcast == null)
                {
                    throw new ArgumentException($"Podcast with id '{podcastId}' not found.");
                }

                logger.LogInformation($"Processing '{podcastId}' : '{podcast.Name}'.");
                if (podcast == null)
                {
                    throw new ArgumentException($"No podcast with id '{podcastId}' found.");
                }

                foreach (var podcastEpisode in podcast.Episodes)
                {
                    if (request.ResetSubjects)
                    {
                        podcastEpisode.Subjects = new List<string>();
                    }

                    var results = await subjectEnricher.EnrichSubjects(
                        podcastEpisode,
                        new SubjectEnrichmentOptions(
                            podcast.IgnoredAssociatedSubjects,
                            podcast.IgnoredSubjects,
                            podcast.DefaultSubject));
                }

                if (request.Commit)
                {
                    await repository.Save(podcast);
                }
            }
        }
        else if (request.CategoriseRecent)
        {
            await recentEpisodeCategoriser.Categorise();
        }
        else
        {
            throw new ArgumentException("Unknown operation", nameof(request));
        }
    }
}