using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.Apple;

public class AppleUrlCategoriser : IAppleUrlCategoriser
{
    private static readonly Regex AppleIds = new(@"podcast/[\w\-]+/id(?'podcastId'\d+)\?i=(?'episodeId'\d+)");
    private readonly IAppleEpisodeResolver _appleEpisodeResolver;
    private readonly IApplePodcastResolver _applePodcastResolver;
    private readonly ILogger<AppleUrlCategoriser> _logger;

    public AppleUrlCategoriser(
        IAppleEpisodeResolver appleEpisodeResolver,
        IApplePodcastResolver applePodcastResolver,
        ILogger<AppleUrlCategoriser> logger)
    {
        _appleEpisodeResolver = appleEpisodeResolver;
        _applePodcastResolver = applePodcastResolver;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("apple");
    }

    public async Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast, IndexingContext indexingContext)
    {
        var podcast =
            await _applePodcastResolver.FindPodcast(new FindApplePodcastRequest(
                matchingPodcast?.AppleId,
                matchingPodcast?.Name ?? criteria.ShowName,
                matchingPodcast?.Publisher ?? criteria.Publisher));

        if (podcast == null)
        {
            _logger.LogWarning($"Could not find podcast with name '{criteria.ShowName}'.");
            return null;
        }

        var episode = await _appleEpisodeResolver.FindEpisode(
            new FindAppleEpisodeRequest(
                podcast.Id,
                podcast.Name,
                null,
                criteria.EpisodeTitle,
                criteria.Release,
                0)
            , indexingContext);

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

        _logger.LogWarning(
            $"Could not find item with episode-title '{criteria.EpisodeTitle}' and for podcast with name '{criteria.ShowName}'.");
        return null;
    }

    public async Task<ResolvedAppleItem> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.Apple == url);
        if (pair != null)
        {
            return new ResolvedAppleItem(pair);
        }

        var podcastIdMatch = AppleIds.Match(url.ToString()).Groups["podcastId"];
        var episodeIdMatch = AppleIds.Match(url.ToString()).Groups["episodeId"];

        if (!episodeIdMatch.Success)
        {
            throw new InvalidOperationException($"Unable to find apple-episode-id in url '{url}'.");
        }

        if (!podcastIdMatch.Success)
        {
            throw new InvalidOperationException($"Unable to find apple-podcast-id in url '{url}'.");
        }

        var podcastId = long.Parse(podcastIdMatch.Value);
        var episodeId = long.Parse(episodeIdMatch.Value);

        var episode = await _appleEpisodeResolver.FindEpisode(
            new FindAppleEpisodeRequest(
                podcastId,
                string.Empty,
                episodeId,
                string.Empty,
                DateTime.MinValue,
                0)
            , indexingContext);

        var podcast =
            await _applePodcastResolver.FindPodcast(new FindApplePodcastRequest(podcastId, string.Empty, string.Empty));

        if (episode != null && podcast != null)
        {
            return new ResolvedAppleItem(
                podcastId,
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

        throw new InvalidOperationException(
            $"Could not find item with apple-episode-id '{episodeId}' and apple-podcast-id '{podcastId}'.");
    }
}