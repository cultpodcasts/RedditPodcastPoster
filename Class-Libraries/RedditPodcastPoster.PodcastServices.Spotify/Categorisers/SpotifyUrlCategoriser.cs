using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Categorisers;

public class SpotifyUrlCategoriser(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    ILogger<SpotifyUrlCategoriser> logger)
    : ISpotifyUrlCategoriser
{
    public async Task<ResolvedSpotifyItem?> Resolve(
        PodcastServiceSearchCriteria criteria,
        Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        var skip = matchingPodcast != null &&
                   matchingPodcast.HasExpensiveSpotifyEpisodesQuery() &&
                   indexingContext.SkipExpensiveSpotifyQueries;
        if (skip)
        {
            logger.LogError("Skipping finding-episode as '{property}' is set.",
                nameof(indexingContext.SkipExpensiveSpotifyQueries));
            return null;
        }

        var result = await FindEpisode(matchingPodcast, criteria, indexingContext);
        if (result == null && !string.IsNullOrWhiteSpace(criteria.AppleTitle))
        {
            var altCriteria = criteria with { EpisodeTitle = criteria.AppleTitle };
            result = await FindEpisode(matchingPodcast, altCriteria, indexingContext);
        }

        if (result != null)
        {
            return result;
        }

        if (!string.IsNullOrWhiteSpace(criteria.AppleTitle))
        {
            logger.LogWarning(
                "Could not find spotify episode for show named '{showName}' and episode-name '{episodeTitle}' or apple-title '{appleTitle}'.",
                criteria.ShowName, criteria.EpisodeTitle, criteria.AppleTitle);
        }
        else
        {
            logger.LogWarning(
                "Could not find spotify episode for show named '{showName}' and episode-name '{episodeTitle}'.",
                criteria.ShowName, criteria.EpisodeTitle);
        }

        return null;
    }

    private async Task<ResolvedSpotifyItem?> FindEpisode(
        Podcast? matchingPodcast,
        PodcastServiceSearchCriteria criteria,
        IndexingContext indexingContext)
    {
        if (matchingPodcast != null &&
            matchingPodcast.IsAwaitingDelayedAudioRelease(criteria.Release, criteria.Duration))
        {
            logger.LogInformation(
                "Skipping Spotify episode lookup for '{CriteriaEpisodeTitle}' as audio is not expected until after the YouTube publishing delay.",
                criteria.EpisodeTitle);
            return null;
        }

        var request = FindSpotifyEpisodeRequestFactory.Create(matchingPodcast, criteria);
        var ticks = EpisodeReleaseTolerance.GetToleranceTicks(
            matchingPodcast,
            criteria.Duration,
            request.YouTubePublishingDelay,
            request.ReleaseAuthority);

        var findEpisodeResponse = await spotifyEpisodeResolver.FindEpisode(
            request,
            indexingContext,
            y => request.Released.HasValue &&
                 Math.Abs((y.GetReleaseDate() - request.Released.Value).Ticks) < ticks);

        if (findEpisodeResponse.FullEpisode == null)
        {
            return null;
        }

        if (!findEpisodeResponse.FullEpisode.IsSpotifyFree())
        {
            logger.LogWarning(
                "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}).",
                findEpisodeResponse.FullEpisode.Id,
                findEpisodeResponse.FullEpisode.Name,
                findEpisodeResponse.FullEpisode.GetSpotifyRestrictionReason());
            return null;
        }

        return new ResolvedSpotifyItem(
            findEpisodeResponse.FullEpisode.Show.Id,
            findEpisodeResponse.FullEpisode.Id,
            findEpisodeResponse.FullEpisode.Show.Name,
            findEpisodeResponse.FullEpisode.Show.Description,
            // Spotify removed 'publisher' from show objects (Feb 2026 Web API changes); keep reading it
            // while responses may still carry it â€” downstream already tolerates null/empty.
#pragma warning disable CS0618
            findEpisodeResponse.FullEpisode.Show.Publisher,
#pragma warning restore CS0618
            findEpisodeResponse.FullEpisode.Name,
            htmlSanitiser.Sanitise(findEpisodeResponse.FullEpisode.HtmlDescription),
            findEpisodeResponse.FullEpisode.GetReleaseDate(),
            findEpisodeResponse.FullEpisode.GetDuration(),
            findEpisodeResponse.FullEpisode.GetUrl(),
            findEpisodeResponse.FullEpisode.Explicit,
            findEpisodeResponse.FullEpisode.GetBestImageUrl());
    }

    public async Task<ResolvedSpotifyItem> Resolve(
        Podcast? podcast,
        IEnumerable<Episode> episodes,
        Uri url,
        IndexingContext indexingContext)
    {
        if (podcast != null && episodes.Any(x => x.Urls.Spotify == url))
        {
            return new ResolvedSpotifyItem(new PodcastEpisode(podcast,
                episodes.Single(x => x.Urls.Spotify == url)));
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
            if (!findEpisodeResponse.FullEpisode.IsSpotifyFree())
            {
                logger.LogWarning(
                    "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}).",
                    findEpisodeResponse.FullEpisode.Id,
                    findEpisodeResponse.FullEpisode.Name,
                    findEpisodeResponse.FullEpisode.GetSpotifyRestrictionReason());
                throw new InvalidOperationException(
                    $"Spotify episode '{episodeId}' is not free/playable.");
            }

            return new ResolvedSpotifyItem(
                findEpisodeResponse.FullEpisode.Show.Id,
                findEpisodeResponse.FullEpisode.Id,
                findEpisodeResponse.FullEpisode.Show.Name,
                findEpisodeResponse.FullEpisode.Show.Description,
                // See note above re: removed 'publisher' field.
#pragma warning disable CS0618
                findEpisodeResponse.FullEpisode.Show.Publisher,
#pragma warning restore CS0618
                findEpisodeResponse.FullEpisode.Name,
                htmlSanitiser.Sanitise(findEpisodeResponse.FullEpisode.HtmlDescription),
                findEpisodeResponse.FullEpisode.GetReleaseDate(),
                findEpisodeResponse.FullEpisode.GetDuration(),
                new Uri(findEpisodeResponse.FullEpisode.ExternalUrls.FirstOrDefault().Value, UriKind.Absolute),
                findEpisodeResponse.FullEpisode.Explicit,
                findEpisodeResponse.FullEpisode.GetBestImageUrl());
        }

        logger.LogError("Skipping finding-episode as '{property}' is set.",
            nameof(indexingContext.SkipExpensiveSpotifyQueries));

        throw new InvalidOperationException($"Could not find item with spotify-id '{episodeId}'.");
    }
}
