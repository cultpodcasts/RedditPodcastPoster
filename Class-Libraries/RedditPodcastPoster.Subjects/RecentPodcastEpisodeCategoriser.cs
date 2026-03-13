using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class RecentPodcastEpisodeCategoriser(
    IPodcastRepositoryV2 podcastRepository,
    IEpisodeRepository episodeRepository,
    ICategoriser categoriser,
    ILogger<RecentPodcastEpisodeCategoriser> logger)
    : IRecentPodcastEpisodeCategoriser
{
    public async Task<IList<Guid>> Categorise()
    {
        var since = DateTimeExtensions.DaysAgo(7);
        IList<Guid> updatedEpisodes = new List<Guid>();

        var uncategorisedByPodcast = new Dictionary<Guid, List<Episode>>();
        await foreach (var episode in episodeRepository.GetAllBy(x => x.Release > since && !x.Subjects.Any()))
        {
            if (!uncategorisedByPodcast.TryGetValue(episode.PodcastId, out var episodes))
            {
                episodes = [];
                uncategorisedByPodcast[episode.PodcastId] = episodes;
            }

            episodes.Add(episode);
        }

        if (!uncategorisedByPodcast.Any())
        {
            return updatedEpisodes;
        }

        var podcastsToCategorise = new List<(Guid Id, string Name, Podcast Podcast, List<Episode> Episodes)>();
        foreach (var (podcastId, episodes) in uncategorisedByPodcast)
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast == null || podcast.Removed == true)
            {
                continue;
            }

            podcastsToCategorise.Add((podcastId, podcast.Name, podcast, episodes));
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