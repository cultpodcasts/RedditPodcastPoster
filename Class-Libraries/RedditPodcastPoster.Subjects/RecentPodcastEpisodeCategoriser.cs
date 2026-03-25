using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.Subjects;

public class RecentPodcastEpisodeCategoriser(
    ICategoriser categoriser,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task<IList<Guid>> Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(postingCriteria.Value.CategoriserDays);
        IList<Guid> updatedEpisodes = new List<Guid>();

        var recentPodcastEpisodes = await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(since);

        var podcastEpisodesToCategorise = recentPodcastEpisodes
            .Where(x => !x.Episode.Subjects.Any())
            .ToList();

        if (!podcastEpisodesToCategorise.Any())
        {
            return updatedEpisodes;
        }

        logger.LogInformation(
            "Categorising podcasts: {podcastNamesAndGuids}",
            string.Join(", ", podcastEpisodesToCategorise
                .GroupBy(x => x.Podcast.Id)
                .Select(g => $"'{g.First().Podcast.Name}' ({g.Key})")));

        foreach (var podcastEpisode in podcastEpisodesToCategorise)
        {
            logger.LogInformation("Categorise podcast '{Name}'.", podcastEpisode.Podcast.Name);
            logger.LogInformation("Categorise episode '{episodeTitle}'.", podcastEpisode.Episode.Title);

            var updatedEpisode = await categoriser.Categorise(
                podcastEpisode.Episode,
                podcastEpisode.Podcast.IgnoredAssociatedSubjects,
                podcastEpisode.Podcast.IgnoredSubjects,
                podcastEpisode.Podcast.DefaultSubject,
                podcastEpisode.Podcast.DescriptionRegex);

            if (updatedEpisode)
            {
                updatedEpisodes.Add(podcastEpisode.Episode.Id);
                logger.LogInformation(
                    "{class}: Podcast '{podcastName}' with id '{podcastId}' and episode with id {episodeId}, updated subjects: {subjects}.",
                    nameof(RecentPodcastEpisodeCategoriser),
                    podcastEpisode.Podcast.Name,
                    podcastEpisode.Podcast.Id,
                    podcastEpisode.Episode.Id,
                    string.Join(", ", podcastEpisode.Episode.Subjects.Select(x => $"'{x}'")));
            }
        }

        return updatedEpisodes;
    }
}