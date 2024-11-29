using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.Apple;

public class AppleUrlCategoriser(
    IAppleEpisodeResolver appleEpisodeResolver,
    IApplePodcastResolver applePodcastResolver,
    ILogger<AppleUrlCategoriser> logger)
    : IAppleUrlCategoriser
{
    public async Task<ResolvedAppleItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var podcast =
            await applePodcastResolver.FindPodcast(new FindApplePodcastRequest(
                matchingPodcast?.AppleId,
                matchingPodcast?.Name ?? criteria.ShowName,
                !string.IsNullOrWhiteSpace(matchingPodcast?.Publisher)
                    ? matchingPodcast.Publisher
                    : criteria.Publisher));

        if (podcast == null)
        {
            logger.LogWarning($"Could not find podcast with name '{criteria.ShowName}'.");
            return null;
        }

        if (matchingPodcast is {AppleId: null})
        {
            matchingPodcast.AppleId = podcast.Id;
        }

        var findEpisodeRequest = FindAppleEpisodeRequestFactory.Create(matchingPodcast, podcast, criteria);

        var episode = await appleEpisodeResolver.FindEpisode(findEpisodeRequest, indexingContext);

        if (episode != null)
        {
            return new ResolvedAppleItem(
                podcast.Id,
                episode.Id,
                podcast.Name,
                podcast.Description,
                podcast.ArtistName,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Duration,
                episode.Url.CleanAppleUrl(),
                episode.Explicit,
                episode.Image);
        }

        logger.LogWarning(
            $"Could not find item with episode-title '{criteria.EpisodeTitle}' and for podcast with name '{criteria.ShowName}'.");
        return null;
    }

    public async Task<ResolvedAppleItem> Resolve(Podcast? podcast, Uri url, IndexingContext indexingContext)
    {
        if (podcast != null && podcast.Episodes.Any(x => x.Urls.Apple == url))
        {
            return new ResolvedAppleItem(new Models.PodcastEpisode(podcast,
                podcast.Episodes.Single(x => x.Urls.Apple == url)));
        }

        var podcastId = AppleIdResolver.GetPodcastId(url);
        var episodeId = AppleIdResolver.GetEpisodeId(url);

        if (episodeId == null)
        {
            throw new InvalidOperationException($"Unable to find apple-episode-id in url '{url}'.");
        }

        if (podcastId == null)
        {
            throw new InvalidOperationException($"Unable to find apple-podcast-id in url '{url}'.");
        }

        var findAppleEpisodeRequest = FindAppleEpisodeRequestFactory.Create(podcastId.Value, episodeId.Value);

        var episode = await appleEpisodeResolver.FindEpisode(findAppleEpisodeRequest, indexingContext);

        var foundPodcast =
            await applePodcastResolver.FindPodcast(new FindApplePodcastRequest(podcastId, string.Empty, string.Empty));

        if (episode != null && foundPodcast != null)
        {
            return new ResolvedAppleItem(
                podcastId,
                episode.Id,
                foundPodcast.Name,
                foundPodcast.Description,
                foundPodcast.ArtistName,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Duration,
                episode.Url.CleanAppleUrl(),
                episode.Explicit,
                episode.Image);
        }

        throw new InvalidOperationException(
            $"Could not find item with apple-episode-id '{episodeId}' and apple-podcast-id '{podcastId}'.");
    }
}