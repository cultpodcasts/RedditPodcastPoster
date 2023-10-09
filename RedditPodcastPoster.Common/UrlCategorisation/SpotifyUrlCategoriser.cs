using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.Spotify;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public class SpotifyUrlCategoriser : ISpotifyUrlCategoriser
{
    private static readonly Regex SpotifyId = new(@"episode/(?'episodeId'\w+)");
    private readonly ILogger<SpotifyUrlCategoriser> _logger;
    private readonly ISpotifyItemResolver _spotifyItemResolver;

    public SpotifyUrlCategoriser(
        ISpotifyItemResolver spotifyItemResolver,
        ILogger<SpotifyUrlCategoriser> logger)
    {
        _spotifyItemResolver = spotifyItemResolver;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("spotify");
    }

    public async Task<ResolvedSpotifyItem> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.Spotify == url);

        if (pair != null)
        {
            return new ResolvedSpotifyItem(pair);
        }

        var episodeId = SpotifyId.Match(url.ToString()).Groups["episodeId"].Value;
        if (episodeId == null)
        {
            throw new InvalidOperationException($"Unable to find spotify-id in url '{url}'.");
        }

        var item = await _spotifyItemResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(episodeId),
            indexingContext);
        if (item != null)
        {
            return new ResolvedSpotifyItem(
                item.Show.Id,
                item.Id,
                item.Show.Name,
                item.Show.Description,
                item.Show.Publisher,
                item.Name,
                item.Description,
                item.GetReleaseDate(),
                item.GetDuration(),
                new Uri(item.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                item.Explicit);
        }

        throw new InvalidOperationException($"Could not find item with spotify-id '{SpotifyId}'.");
    }

    public async Task<ResolvedSpotifyItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var request = new FindSpotifyEpisodeRequest(
            matchingPodcast?.SpotifyId ?? string.Empty,
            (matchingPodcast?.Name ?? criteria.ShowName).Trim(),
            string.Empty,
            criteria.EpisodeTitle.Trim());
        var item = await _spotifyItemResolver.FindEpisode(request, indexingContext);
        if (item != null)
        {
            return new ResolvedSpotifyItem(
                item.Show.Id,
                item.Id,
                item.Show.Name,
                item.Show.Description,
                item.Show.Publisher,
                item.Name,
                item.Description,
                item.GetReleaseDate(),
                item.GetDuration(),
                item.GetUrl(),
                item.Explicit);
        }

        _logger.LogWarning(
            $"Could not find spotify episode for show named '{criteria.ShowName}' and episode-name '{criteria.EpisodeTitle}'.");
        return null;
    }
}