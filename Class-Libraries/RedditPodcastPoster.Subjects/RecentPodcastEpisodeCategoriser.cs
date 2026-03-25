using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class RecentPodcastEpisodeCategoriser(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
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

        var recentEpisodes = await recentEpisodeCandidatesProvider.GetRecentActiveEpisodes(since);

        var podcastsToCategorise = new List<(Guid Id, string Name, Podcast Podcast, List<Episode> Episodes)>();
        foreach (var episodeGroup in recentEpisodes.GroupBy(x => x.PodcastId))
        {
            var podcast = await podcastRepository.GetPodcast(episodeGroup.Key);
            if (podcast == null || podcast.Removed == true)
            {
                continue;
            }

            var episodes = episodeGroup
                .Where(x => !x.Subjects.Any())
                .ToList();

            if (!episodes.Any())
            {
                continue;
            }

            podcastsToCategorise.Add((episodeGroup.Key, podcast.Name, podcast, episodes));
        }

        if (!podcastsToCategorise.Any())
        {
            return updatedEpisodes;
        }

        logger.LogInformation(
            "Categorising podcasts: {podcastNamesAndGuids}",
            string.Join(", ", podcastsToCategorise.Select(x => $"'{x.Name}' ({x.Id})")));

        foreach (var podcastDetails in podcastsToCategorise)
        {
            logger.LogInformation("Categorise podcast '{Name}'.", podcastDetails.Name);

            foreach (var episode in podcastDetails.Episodes)
            {
                logger.LogInformation("Categorise episode '{episodeTitle}'.", episode.Title);
                var updatedEpisode = await categoriser.Categorise(
                    episode,
                    podcastDetails.Podcast.IgnoredAssociatedSubjects,
                    podcastDetails.Podcast.IgnoredSubjects,
                    podcastDetails.Podcast.DefaultSubject,
                    podcastDetails.Podcast.DescriptionRegex);

                if (updatedEpisode)
                {
                    await episodeRepository.Save(episode);
                    updatedEpisodes.Add(episode.Id);
                    logger.LogInformation(
                        "{class}: Podcast '{podcastName}' with id '{podcastId}' and episode with id {episodeId}, updated subjects: {subjects}.",
                        nameof(RecentPodcastEpisodeCategoriser),
                        podcastDetails.Podcast.Name,
                        podcastDetails.Podcast.Id,
                        episode.Id,
                        string.Join(", ", episode.Subjects.Select(x => $"'{x}'")));
                }
            }
        }

        return updatedEpisodes;
    }
}