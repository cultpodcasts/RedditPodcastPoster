using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects;

namespace CategorisePodcastEpisodes;

public class CategorisePodcastEpisodesProcessor(
    IPodcastRepository repository,
    ISubjectEnricher subjectEnricher,
    ILogger<CategorisePodcastEpisodesProcessor> logger)
{
    public async Task Run(CategorisePodcastEpisodesRequest request)
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

                await subjectEnricher.EnrichSubjects(
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
}