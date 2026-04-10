using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class RecentPodcastEpisodeCategoriser(
    ICategoriser categoriser,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IEpisodeRepository episodeRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task<IList<Guid>> Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(postingCriteria.Value.CategoriserDays);
        IList<Guid> updatedEpisodes = new List<Guid>();

        var recentPodcastEpisodes = await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(since);

        logger.LogWarning("{method}: Retrieved recent active episodes. Count: {Count}. {json}", nameof(Categorise),
            recentPodcastEpisodes.Count,
            JsonSerializer.Serialize(recentPodcastEpisodes.Select(x => new
                { x.Podcast.Name, x.Episode.Title, x.Episode.Id })));

        var podcastEpisodesToCategorise = recentPodcastEpisodes
            .Where(x => !x.Episode.Subjects.Any())
            .ToList();

        logger.LogWarning("{method}: Consider for categorisation. Count: {Count}. {json}", nameof(Categorise),
            podcastEpisodesToCategorise.Count,
            JsonSerializer.Serialize(
                podcastEpisodesToCategorise.Select(x => new { x.Podcast.Name, x.Episode.Title, x.Episode.Id })));


        if (!podcastEpisodesToCategorise.Any())
        {
            return updatedEpisodes;
        }

        logger.LogWarning(
            "{method}: Categorising podcasts: {podcastNamesAndGuids}",
            nameof(Categorise),
            string.Join(", ", podcastEpisodesToCategorise
                .GroupBy(x => x.Podcast.Id)
                .Select(g => $"'{g.First().Podcast.Name}' ({g.Key})")));

        foreach (var podcastEpisode in podcastEpisodesToCategorise)
        {
            logger.LogWarning("{method}: Categorise podcast '{Name}'.", nameof(Categorise),
                podcastEpisode.Podcast.Name);
            logger.LogWarning("{method}: Categorise episode '{episodeTitle}'.", nameof(Categorise),
                podcastEpisode.Episode.Title);

            var updatedEpisode = await categoriser.Categorise(
                podcastEpisode.Episode,
                podcastEpisode.Podcast.IgnoredAssociatedSubjects,
                podcastEpisode.Podcast.IgnoredSubjects,
                podcastEpisode.Podcast.DefaultSubject,
                podcastEpisode.Podcast.DescriptionRegex);

            if (updatedEpisode)
            {
                await episodeRepository.Save(podcastEpisode.Episode);

                updatedEpisodes.Add(podcastEpisode.Episode.Id);
                logger.LogWarning(
                    "{method}: Podcast '{podcastName}' with id '{podcastId}' and episode with id {episodeId}, updated subjects persisted: {subjects}.",
                    nameof(Categorise),
                    podcastEpisode.Podcast.Name,
                    podcastEpisode.Podcast.Id,
                    podcastEpisode.Episode.Id,
                    string.Join(", ", podcastEpisode.Episode.Subjects.Select(x => $"'{x}'")));
            }
        }

        return updatedEpisodes;
    }
}