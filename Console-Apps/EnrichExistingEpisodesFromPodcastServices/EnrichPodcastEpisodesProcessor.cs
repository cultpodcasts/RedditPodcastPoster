using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.Text;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesProcessor(
    IPodcastRepositoryV2 podcastsRepository,
    IEpisodeRepository episodeRepository,
    ISpotifyUrlCategoriser spotifyUrlCategoriser,
    IAppleUrlCategoriser appleUrlCategoriser,
    IYouTubeUrlCategoriser youTubeUrlCategoriser,
    ISpotifyEpisodeResolver spotifyEpisodeResolver,
    IAppleEpisodeResolver appleEpisodeResolver,
    IHtmlSanitiser htmlSanitiser,
    IEpisodeSearchIndexerService episodeSearchIndexerService,
    ILogger<EnrichPodcastEpisodesProcessor> logger)
{
    public async Task Run(EnrichPodcastEpisodesRequest request)
    {
        IndexingContext indexingContext;
        List<Guid> updatedEpisodeIds = new();
        if (request.ReleasedSince.HasValue)
        {
            indexingContext = new IndexingContext(DateTimeExtensions.DaysAgo(request.ReleasedSince.Value));
        }
        else
        {
            indexingContext = new IndexingContext();
        }

        indexingContext = indexingContext with
        {
            SkipExpensiveSpotifyQueries = !request.AllowExpensiveQueries,
            SkipExpensiveYouTubeQueries = !request.AllowExpensiveQueries,
            SkipYouTubeUrlResolving = request.SkipYouTubeUrlResolving
        };

        Guid podcastId;
        if (request.PodcastId.HasValue)
        {
            podcastId = request.PodcastId.Value;
        }
        else if (request.PodcastName != null)
        {
            var podcastIds = await podcastsRepository.GetAllBy(x =>
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase))
                .Select(x => x.Id)
                .ToListAsync();
            if (!podcastIds.Any())
            {
                throw new InvalidOperationException($"No podcast matching '{request.PodcastName}' could be found.");
            }

            if (podcastIds.Count() > 1)
            {
                throw new InvalidOperationException(
                    $"Multiple podcasts matching '{request.PodcastName}' were found. Ids: {string.Join(", ", podcastIds)}.");
            }

            podcastId = podcastIds.First();
        }
        else
        {
            throw new InvalidOperationException("A podcast-id or podcast-name must be provided.");
        }

        var podcast = await podcastsRepository.GetPodcast(podcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast found with id '{request.PodcastId}'.");
        }

        var episodesQuery = episodeRepository.GetByPodcastId(podcastId);
        if (request.ReleasedSince.HasValue)
        {
            episodesQuery = episodeRepository.GetByPodcastId(
                podcastId,
                x => x.Release >= indexingContext.ReleasedSince);
        }

        var currentEpisodes = await episodeRepository.GetByPodcastId(podcastId).ToListAsync();

        await foreach (var detachedEpisode in episodesQuery)
        {
            var episodeUpdated = false;
            var criteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty, podcast.Publisher,
                detachedEpisode.Title, detachedEpisode.Description, detachedEpisode.Release, detachedEpisode.Length);

            if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                !string.IsNullOrWhiteSpace(detachedEpisode.SpotifyId) &&
                detachedEpisode.AppleId == null)
            {
                var spotifyEpisode =
                    await spotifyEpisodeResolver.FindEpisode(
                        FindSpotifyEpisodeRequestFactory.Create(podcast, detachedEpisode),
                        indexingContext);
                if (spotifyEpisode?.FullEpisode != null &&
                    spotifyEpisode.FullEpisode.Name.Trim() != detachedEpisode.Title.Trim())
                {
                    criteria.SpotifyTitle = spotifyEpisode.FullEpisode.Name.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                podcast.AppleId != null &&
                detachedEpisode.AppleId != null &&
                string.IsNullOrWhiteSpace(detachedEpisode.SpotifyId))
            {
                var appleEpisode =
                    await appleEpisodeResolver.FindEpisode(
                        FindAppleEpisodeRequestFactory.Create(podcast, detachedEpisode),
                        indexingContext);
                if (appleEpisode != null && appleEpisode.Title.Trim() != detachedEpisode.Title.Trim())
                {
                    criteria.AppleTitle = appleEpisode.Title.Trim();
                }
            }

            if (podcast.AppleId != null && (detachedEpisode.AppleId == null || detachedEpisode.Urls.Apple == null))
            {
                var match = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    detachedEpisode.Urls.Apple ??= match.Url;
                    detachedEpisode.AppleId ??= match.EpisodeId;
                    var appleImage = match.Image;
                    if (appleImage != null)
                    {
                        detachedEpisode.Images ??= new EpisodeImages();
                        detachedEpisode.Images.Apple = appleImage;
                    }

                    logger.LogInformation("Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                        match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((!string.IsNullOrWhiteSpace(detachedEpisode.SpotifyId) ||
                         detachedEpisode.Urls.Spotify != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var spotifyEpisode =
                            await spotifyEpisodeResolver.FindEpisode(
                                FindSpotifyEpisodeRequestFactory.Create(podcast, detachedEpisode), indexingContext);
                        if (spotifyEpisode.FullEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty,
                                podcast.Publisher, spotifyEpisode.FullEpisode.Name,
                                htmlSanitiser.Sanitise(spotifyEpisode.FullEpisode.HtmlDescription),
                                spotifyEpisode.FullEpisode.GetReleaseDate(),
                                spotifyEpisode.FullEpisode.GetDuration());
                            match = await appleUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                detachedEpisode.Urls.Apple ??= match.Url;
                                detachedEpisode.AppleId ??= match.EpisodeId;
                                var appleImage = match.Image;
                                if (appleImage != null)
                                {
                                    detachedEpisode.Images ??= new EpisodeImages();
                                    detachedEpisode.Images.Apple = appleImage;
                                }

                                logger.LogInformation(
                                    "Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId,
                                    match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (podcast.YouTubeChannelId != null &&
                (string.IsNullOrWhiteSpace(detachedEpisode.YouTubeId) || detachedEpisode.Urls.YouTube == null))
            {
                if (string.IsNullOrWhiteSpace(detachedEpisode.YouTubeId) && detachedEpisode.Urls.YouTube != null)
                {
                    var youTubeId = YouTubeIdResolver.Extract(detachedEpisode.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        detachedEpisode.YouTubeId = youTubeId;
                        logger.LogInformation(
                            "Enriched from youtube-url: '{UrlsYouTube}', youtube-id: '{EpisodeYouTubeId}'.",
                            detachedEpisode.Urls.YouTube, detachedEpisode.YouTubeId);
                    }
                }
                else if (detachedEpisode.Urls.YouTube == null && !string.IsNullOrWhiteSpace(detachedEpisode.YouTubeId))
                {
                    detachedEpisode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(detachedEpisode.YouTubeId);
                    logger.LogInformation(
                        "Enriched from youtube-id: '{EpisodeYouTubeId}', Url: '{UrlsYouTube}'.",
                        detachedEpisode.YouTubeId,
                        detachedEpisode.Urls.YouTube);
                }
                else
                {
                    var match = await youTubeUrlCategoriser.Resolve(criteria, podcast, currentEpisodes,
                        indexingContext);
                    if (match != null)
                    {
                        detachedEpisode.Urls.YouTube ??= match.Url;
                        if (string.IsNullOrWhiteSpace(detachedEpisode.YouTubeId))
                        {
                            detachedEpisode.YouTubeId = match.EpisodeId;
                        }

                        var youTubeImage = match.Image;
                        if (youTubeImage != null)
                        {
                            detachedEpisode.Images ??= new EpisodeImages();
                            detachedEpisode.Images.YouTube = youTubeImage;
                        }

                        logger.LogInformation(
                            "Enriched episode with episode-id '{EpisodeId}' from youtube: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                            detachedEpisode.Id, match.EpisodeId, match.Url);
                        episodeUpdated = true;
                    }
                }
            }

            if (podcast.SpotifyId != null &&
                (string.IsNullOrWhiteSpace(detachedEpisode.SpotifyId) || detachedEpisode.Urls.Spotify == null))
            {
                var match = await spotifyUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    detachedEpisode.Urls.Spotify ??= match.Url;
                    if (string.IsNullOrWhiteSpace(detachedEpisode.SpotifyId))
                    {
                        detachedEpisode.SpotifyId = match.EpisodeId;
                    }

                    var spotifyImage = match.Image;
                    if (spotifyImage != null)
                    {
                        detachedEpisode.Images ??= new EpisodeImages();
                        detachedEpisode.Images.Spotify = spotifyImage;
                    }

                    logger.LogInformation("Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                        match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((detachedEpisode.AppleId != null || detachedEpisode.Urls.Apple != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var appleEpisode =
                            await appleEpisodeResolver.FindEpisode(
                                FindAppleEpisodeRequestFactory.Create(podcast, detachedEpisode), indexingContext);
                        if (appleEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty,
                                podcast.Publisher, appleEpisode.Title, appleEpisode.Description,
                                appleEpisode.Release,
                                appleEpisode.Duration);
                            match = await spotifyUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                detachedEpisode.Urls.Spotify ??= match.Url;
                                detachedEpisode.SpotifyId = match.EpisodeId;
                                var spotifyImage = match.Image;
                                if (spotifyImage != null)
                                {
                                    detachedEpisode.Images ??= new EpisodeImages();
                                    detachedEpisode.Images.Spotify = spotifyImage;
                                }

                                logger.LogInformation(
                                    "Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                                    match.EpisodeId,
                                    match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (episodeUpdated)
            {
                await episodeRepository.Save(detachedEpisode);
                updatedEpisodeIds.Add(detachedEpisode.Id);
            }
        }

        if (updatedEpisodeIds.Any())
        {
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}