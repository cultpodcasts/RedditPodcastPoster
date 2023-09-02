using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public class UrlCategoriser : IUrlCategoriser
{
    private readonly IAppleUrlCategoriser _appleUrlCategoriser;
    private readonly ILogger<UrlCategoriser> _logger;
    private readonly ISpotifyUrlCategoriser _spotifyUrlCategoriser;
    private readonly IYouTubeUrlCategoriser _youTubeUrlCategoriser;

    public UrlCategoriser(
        ISpotifyUrlCategoriser spotifyUrlCategoriser,
        IAppleUrlCategoriser appleUrlCategoriser,
        IYouTubeUrlCategoriser youTubeUrlCategoriser,
        ILogger<UrlCategoriser> logger)
    {
        _spotifyUrlCategoriser = spotifyUrlCategoriser;
        _appleUrlCategoriser = appleUrlCategoriser;
        _youTubeUrlCategoriser = youTubeUrlCategoriser;
        _logger = logger;
    }

    public async Task<CategorisedItem> Categorise(Uri url)
    {
        ResolvedSpotifyItem? resolvedSpotifyItem = null;
        ResolvedAppleItem? resolvedAppleItem = null;
        ResolvedYouTubeItem? resolvedYouTubeItem = null;
        PodcastServiceSearchCriteria? criteria = null;

        if (_spotifyUrlCategoriser.IsMatch(url))
        {
            resolvedSpotifyItem = await _spotifyUrlCategoriser.Resolve(url);
            criteria = resolvedSpotifyItem.ToPodcastServiceSearchCriteria();
        }
        else if (_appleUrlCategoriser.IsMatch(url))
        {
            resolvedAppleItem = await _appleUrlCategoriser.Resolve(url);
            criteria = resolvedAppleItem.ToPodcastServiceSearchCriteria();
        }
        else if (_youTubeUrlCategoriser.IsMatch(url))
        {
            resolvedYouTubeItem = await _youTubeUrlCategoriser.Resolve(url);
            criteria = resolvedYouTubeItem.ToPodcastServiceSearchCriteria();
        }

        if (criteria != null)
        {
            if (resolvedSpotifyItem == null && !_spotifyUrlCategoriser.IsMatch(url))
            {
                resolvedSpotifyItem = await _spotifyUrlCategoriser.Resolve(criteria);
            }

            if (resolvedAppleItem == null && !_appleUrlCategoriser.IsMatch(url))
            {
                resolvedAppleItem = await _appleUrlCategoriser.Resolve(criteria);
            }

            if (resolvedYouTubeItem == null && !_youTubeUrlCategoriser.IsMatch(url))
            {
                resolvedYouTubeItem = await _youTubeUrlCategoriser.Resolve(criteria);
            }

            return new CategorisedItem(resolvedSpotifyItem, resolvedAppleItem, resolvedYouTubeItem);
        }

        throw new InvalidOperationException($"Could not find episode with url '{url}'.");
    }
}