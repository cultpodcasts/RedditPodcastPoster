using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.Updaters;

/// <inheritdoc cref="IEpisodeServiceMaterialiser"/>
/// <remarks>
/// Each platform is fetched by its own id only (never a title match): Spotify through
/// <see cref="ISpotifyEpisodeResolver"/> in the Index flow with no show id, so a stale id is a clean miss rather
/// than a title search; Apple through <see cref="IApplePodcastService.GetEpisode(long, IndexingContext, CancellationToken)"/>
/// (amp-api by episode id, no show id needed); YouTube through <see cref="IYouTubeVideoService"/> and
/// <see cref="IYouTubeThumbnailResolver"/> (same image rule as <see cref="ImageUpdater"/>). A returned item whose id
/// differs from the requested id is ignored.
/// </remarks>
public class EpisodeServiceMaterialiser(
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IApplePodcastService applePodcastService,
    IYouTubeServiceWrapper youTubeService,
    IYouTubeVideoService youTubeVideoService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    ILogger<EpisodeServiceMaterialiser> logger) : IEpisodeServiceMaterialiser
{
    private sealed record Found(Uri? Url, Uri? Image);

    public async Task MaterialiseAsync(Podcast podcast, IEnumerable<Episode> episodes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episodes);
        foreach (var episode in episodes)
        {
            await MaterialiseAsync(podcast, episode, cancellationToken);
        }
    }

    public async Task MaterialiseAsync(Podcast podcast, Episode episode, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(podcast);
        ArgumentNullException.ThrowIfNull(episode);
        foreach (var key in EpisodeServicePresence.KeysWithIdButNoUrl(episode))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Found? found = null;
            try
            {
                found = key switch
                {
                    ServiceKeys.Spotify => await FindSpotify(podcast, episode, cancellationToken),
                    ServiceKeys.Apple => await FindApple(episode, cancellationToken),
                    ServiceKeys.YouTube => await FindYouTube(episode, cancellationToken),
                    _ => null
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "{method}: '{service}' lookup failed for episode '{episodeId}'; using the id-derived url.",
                    nameof(MaterialiseAsync), key, episode.Id);
            }

            if (found is { Url: not null } or { Image: not null })
            {
                // Client image wins; an image already on services is kept when the client returns none.
                EpisodeServicePresence.Upsert(episode, key, found.Url, found.Image);
            }
            else
            {
                logger.LogInformation(
                    "{method}: '{service}' client returned nothing for episode '{episodeId}'; using the id-derived url.",
                    nameof(MaterialiseAsync), key, episode.Id);
            }
        }

        // Whatever a client could not supply (skipped, stale id, outage) still becomes a Services entry.
        EpisodeServicePresence.EnsureServiceUrlsFromIds(episode, podcast.AppleId);
    }

    private async Task<Found?> FindSpotify(Podcast podcast, Episode episode, CancellationToken cancellationToken)
    {
        var id = EpisodeServicePresence.SpotifyEpisodeId(episode)!;
        var request = FindSpotifyEpisodeRequestFactory.Create(id) with { Market = podcast.SpotifyMarket };
        var context = EpisodeLookupUseCases.ForIndex(new IndexingContext());
        var response = await spotifyEpisodeResolver.FindEpisode(request, context, _ => false, cancellationToken);
        var full = response.FullEpisode;
        if (full is null || !string.Equals(full.Id, id, StringComparison.Ordinal))
        {
            return null;
        }

        var url = full.ExternalUrls?.FirstOrDefault().Value is { Length: > 0 } external &&
                  Uri.TryCreate(external, UriKind.Absolute, out var parsed)
            ? parsed
            : null;
        var image = full.Images is { Count: > 0 } ? full.GetBestImageUrl() : null;
        return new Found(url, image);
    }

    private async Task<Found?> FindApple(Episode episode, CancellationToken cancellationToken)
    {
        var id = EpisodeServicePresence.AppleEpisodeId(episode)!.Value;
        var apple = await applePodcastService.GetEpisode(id, new IndexingContext(), cancellationToken);
        return apple is null || apple.Id != id ? null : new Found(apple.Url, apple.Image);
    }

    private async Task<Found?> FindYouTube(Episode episode, CancellationToken cancellationToken)
    {
        var id = episode.Ids!.YouTube!;
        var videos = await youTubeVideoService.GetVideoContentDetails(
            youTubeService, [id], new IndexingContext(), true);
        var video = videos?.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
        if (video is null)
        {
            return null;
        }

        var image = await youTubeThumbnailResolver.GetImageUrlAsync(video, cancellationToken);
        return new Found(new Uri($"https://www.youtube.com/watch?v={id}"), image);
    }
}
