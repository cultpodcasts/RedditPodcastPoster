using System.Text.RegularExpressions;
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
    private static readonly Regex AppleIds = new(@"podcast/[\w\-]+/id(?'podcastId'\d+)\?i=(?'episodeId'\d+)");

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("apple");
    }

    public async Task<ResolvedAppleItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var podcast =
            await applePodcastResolver.FindPodcast(new FindApplePodcastRequest(
                matchingPodcast?.AppleId,
                matchingPodcast?.Name ?? criteria.ShowName,
                matchingPodcast?.Publisher ?? criteria.Publisher));

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
                episode.Explicit);
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

        var podcastId = GetPodcastId(url);
        var episodeId = GetEpisodeId(url);

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
                episode.Explicit);
        }

        throw new InvalidOperationException(
            $"Could not find item with apple-episode-id '{episodeId}' and apple-podcast-id '{podcastId}'.");
    }

    public long? GetEpisodeId(Uri url)
    {
        var match = AppleIds.Match(url.ToString()).Groups["episodeId"];
        return match.Success ? long.Parse(match.Value) : null;
    }

    public long? GetPodcastId(Uri url)
    {
        var match = AppleIds.Match(url.ToString()).Groups["podcastId"];
        return match.Success ? long.Parse(match.Value) : null;
    }
}