using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.PodcastServices.Spotify;

public class SpotifyUrlCategoriser(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifyUrlCategoriser> logger)
    : ISpotifyUrlCategoriser
{
    public async Task<ResolvedSpotifyItem> Resolve(
        Podcast? podcast, 
        Uri url, 
        IndexingContext indexingContext)
    {
        if (podcast != null && podcast.Episodes.Any(x => x.Urls.Spotify == url))
        {
            return new ResolvedSpotifyItem(new PodcastEpisode(podcast,
                podcast.Episodes.Single(x => x.Urls.Spotify == url)));
        }

        var episodeId = SpotifyIdResolver.GetEpisodeId(url);
        if (episodeId == null)
        {
            throw new InvalidOperationException($"Unable to find spotify-id in url '{url}'.");
        }

        var findEpisodeResponse = await spotifyEpisodeResolver.FindEpisode(
            FindSpotifyEpisodeRequestFactory.Create(episodeId),
            indexingContext);
        if (findEpisodeResponse.FullEpisode != null)
        {
            return new ResolvedSpotifyItem(
                findEpisodeResponse.FullEpisode.Show.Id,
                findEpisodeResponse.FullEpisode.Id,
                findEpisodeResponse.FullEpisode.Show.Name,
                findEpisodeResponse.FullEpisode.Show.Description,
                findEpisodeResponse.FullEpisode.Show.Publisher,
                findEpisodeResponse.FullEpisode.Name,
                htmlSanitiser.Sanitise(findEpisodeResponse.FullEpisode.HtmlDescription),
                findEpisodeResponse.FullEpisode.GetReleaseDate(),
                findEpisodeResponse.FullEpisode.GetDuration(),
                new Uri(findEpisodeResponse.FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                findEpisodeResponse.FullEpisode.Explicit,
                findEpisodeResponse.FullEpisode.GetBestImageUrl());
        }

        logger.LogError("Skipping finding-episode as '{property}' is set.", nameof(indexingContext.SkipExpensiveSpotifyQueries));

        throw new InvalidOperationException($"Could not find item with spotify-id '{episodeId}'.");
    }

    public async Task<ResolvedSpotifyItem?> Resolve(
        PodcastServiceSearchCriteria criteria, 
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var request = FindSpotifyEpisodeRequestFactory.Create(matchingPodcast, criteria);
        if (!indexingContext.SkipExpensiveSpotifyQueries)
        {
            var findEpisodeResponse = await spotifyEpisodeResolver.FindEpisode(request, indexingContext);

            if (findEpisodeResponse.FullEpisode== null && !string.IsNullOrWhiteSpace(criteria.AppleTitle))
            {
                var altCriteria = criteria with { EpisodeTitle = criteria.AppleTitle };
                request = FindSpotifyEpisodeRequestFactory.Create(matchingPodcast, altCriteria);
                findEpisodeResponse = await spotifyEpisodeResolver.FindEpisode(request, indexingContext);
            }

            if (findEpisodeResponse.FullEpisode != null)
            {
                return new ResolvedSpotifyItem(
                    findEpisodeResponse.FullEpisode.Show.Id,
                    findEpisodeResponse.FullEpisode.Id,
                    findEpisodeResponse.FullEpisode.Show.Name,
                    findEpisodeResponse.FullEpisode.Show.Description,
                    findEpisodeResponse.FullEpisode.Show.Publisher,
                    findEpisodeResponse.FullEpisode.Name,
                    htmlSanitiser.Sanitise(findEpisodeResponse.FullEpisode.HtmlDescription),
                    findEpisodeResponse.FullEpisode.GetReleaseDate(),
                    findEpisodeResponse.FullEpisode.GetDuration(),
                    findEpisodeResponse.FullEpisode.GetUrl(),
                    findEpisodeResponse.FullEpisode.Explicit,
                    findEpisodeResponse.FullEpisode.GetBestImageUrl());
            }
        }
        else
        {
            logger.LogError("Skipping finding-episode as '{property}' is set.", nameof(indexingContext.SkipExpensiveSpotifyQueries));
        }

        if (!string.IsNullOrWhiteSpace(criteria.AppleTitle))
        {
            logger.LogWarning("Could not find spotify episode for show named '{showName}' and episode-name '{episodeTitle}' or apple-title '{appleTitle}'.",
                criteria.ShowName, criteria.EpisodeTitle, criteria.AppleTitle);
        }
        else
        {
            logger.LogWarning("Could not find spotify episode for show named '{showName}' and episode-name '{episodeTitle}'.",
                criteria.ShowName, criteria.EpisodeTitle);
        }
        return null;
    }
}