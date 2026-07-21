using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Subjects.Categorisation;

public class RecentPodcastEpisodeCategoriser(
    ICategoriser categoriser,
    IRecentEpisodeCandidatesProvider recentEpisodeCandidatesProvider,
    IEpisodeRepository episodeRepository,
    IOptions<PostingCriteria> postingCriteria,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task<IList<Guid>> Categorise(IReadOnlyList<PodcastEpisode>? preloadedRecentCandidates = null)
    {
        var since = DateTimeExtensions.DaysAgo(postingCriteria.Value.CategoriserDays);
        IList<Guid> updatedEpisodes = new List<Guid>();

        var recentPodcastEpisodes = preloadedRecentCandidates != null
            ? preloadedRecentCandidates.Where(x => x.Episode.Release >= since).ToList()
            : await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(since);

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

        foreach (var podcastGroup in podcastEpisodesToCategorise.GroupBy(x => x.Podcast.Id))
        {
            var podcast = podcastGroup.First().Podcast;
            var episodeDeltas = new List<CategoriseEpisodeDelta>();

            foreach (var podcastEpisode in podcastGroup)
            {
                var episode = podcastEpisode.Episode;
                var before = episode.Subjects.ToArray();

                var updatedEpisode = await categoriser.Categorise(
                    episode,
                    podcast.IgnoredAssociatedSubjects,
                    podcast.IgnoredSubjects,
                    podcast.DefaultSubject,
                    podcast.DescriptionRegex);

                if (updatedEpisode)
                {
                    await episodeRepository.Save(episode);
                    updatedEpisodes.Add(episode.Id);
                }

                episodeDeltas.Add(CategoriseEpisodeDelta.From(
                    episode.Id,
                    episode.Title,
                    before,
                    episode.Subjects.ToArray(),
                    updatedEpisode));
            }

            CategorisePodcastLogger.Log(logger, podcast.Id, podcast.Name, episodeDeltas);
        }

        return updatedEpisodes;
    }
}
