using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Apple;
using RedditPodcastPoster.Common.PodcastServices.Spotify;

namespace RedditPodcastPoster.Common.UrlCategorisation;

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

    public async Task<ResolvedAppleItem> Resolve(Uri url)
    {
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
                0));

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
                TimeSpan.FromMilliseconds(episode.LengthMs),
                AppleUrlResolver.CleanUrl(episode.Url),
                episode.Explicit);
        }

        throw new InvalidOperationException(
            $"Could not find item with apple-episode-id '{episodeId}' and apple-podcast-id '{podcastId}'.");
    }

    public async Task<ResolvedAppleItem?> Resolve(PodcastServiceSearchCriteria criteria)
    {
        var podcast =
            await _applePodcastResolver.FindPodcast(new FindApplePodcastRequest(null, criteria.ShowName,
                criteria.Publisher));


        if (podcast == null)
        {
            _logger.LogWarning($"Could not find podcast with name '{criteria.ShowName}'.");
            return null;
        }

        var episode = await _appleEpisodeResolver.FindEpisode(
            new FindAppleEpisodeRequest(
                podcast.Id,
                criteria.ShowName,
                null,
                criteria.EpisodeTitle,
                criteria.Release,
                0));

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
                AppleUrlResolver.CleanUrl(episode.Url),
                episode.Explicit);
        }

        _logger.LogWarning($"Could not find item with episode-title '{criteria.EpisodeTitle}' and for podcast with name '{criteria.ShowName}'.");
        return null;
    }
}