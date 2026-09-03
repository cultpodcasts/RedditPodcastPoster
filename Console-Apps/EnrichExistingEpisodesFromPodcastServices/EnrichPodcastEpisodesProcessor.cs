using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Categorisers;
using RedditPodcastPoster.PodcastServices.Apple.Factories;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;
using RedditPodcastPoster.PodcastServices.Spotify.Categorisers;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Factories;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.Text.Sanitisers;

namespace EnrichExistingEpisodesFromPodcastServices;

public class EnrichPodcastEpisodesProcessor(
    IPodcastRepository podcastsRepository,
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
                !string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(detachedEpisode)) &&
                EpisodeServicePresence.AppleEpisodeId(detachedEpisode) is null)
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
                EpisodeServicePresence.AppleEpisodeId(detachedEpisode) is not null &&
                string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(detachedEpisode)))
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

            if (podcast.AppleId != null &&
                (EpisodeServicePresence.AppleEpisodeId(detachedEpisode) is null ||
                 !EpisodeServicePresence.HasUrl(detachedEpisode, ServiceKeys.Apple)))
            {
                var match = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    ApplyAppleMatch(detachedEpisode, match);

                    logger.LogInformation("Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                        match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((!string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(detachedEpisode)) ||
                         EpisodeServicePresence.HasUrl(detachedEpisode, ServiceKeys.Spotify)) &&
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
                                ApplyAppleMatch(detachedEpisode, match);

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
                (string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(detachedEpisode)) ||
                 !EpisodeServicePresence.HasUrl(detachedEpisode, ServiceKeys.YouTube)))
            {
                var existingYouTubeUrl = EpisodeServicePresence.TryGetUrl(detachedEpisode, ServiceKeys.YouTube);
                if (string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(detachedEpisode)) &&
                    existingYouTubeUrl != null)
                {
                    var youTubeId = YouTubeIdResolver.Extract(existingYouTubeUrl);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        EpisodeServicePresence.SetYouTubeIdentity(detachedEpisode, youTubeId);
                        logger.LogInformation(
                            "Enriched from youtube-url: '{UrlsYouTube}', youtube-id: '{EpisodeYouTubeId}'.",
                            existingYouTubeUrl, youTubeId);
                    }
                }
                else if (existingYouTubeUrl == null &&
                         !string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(detachedEpisode)))
                {
                    var youTubeUrl = SearchResultExtensions.ToYouTubeUrl(
                        EpisodeServicePresence.YouTubeEpisodeId(detachedEpisode)!);
                    EpisodeServicePresence.Upsert(detachedEpisode, ServiceKeys.YouTube, youTubeUrl, null);
                    logger.LogInformation(
                        "Enriched from youtube-id: '{EpisodeYouTubeId}', Url: '{UrlsYouTube}'.",
                        EpisodeServicePresence.YouTubeEpisodeId(detachedEpisode),
                        youTubeUrl);
                }
                else
                {
                    var match = await youTubeUrlCategoriser.Resolve(criteria, podcast, currentEpisodes,
                        indexingContext);
                    if (match != null)
                    {
                        ApplyYouTubeMatch(detachedEpisode, match);

                        logger.LogInformation(
                            "Enriched episode with episode-id '{EpisodeId}' from youtube: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                            detachedEpisode.Id, match.EpisodeId, match.Url);
                        episodeUpdated = true;
                    }
                }
            }

            if (podcast.SpotifyId != null &&
                (string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(detachedEpisode)) ||
                 !EpisodeServicePresence.HasUrl(detachedEpisode, ServiceKeys.Spotify)))
            {
                var match = await spotifyUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    ApplySpotifyMatch(detachedEpisode, match);

                    logger.LogInformation("Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.",
                        match.EpisodeId,
                        match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((EpisodeServicePresence.AppleEpisodeId(detachedEpisode) is not null ||
                         EpisodeServicePresence.HasUrl(detachedEpisode, ServiceKeys.Apple)) &&
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
                                ApplySpotifyMatch(detachedEpisode, match);

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

    private static void ApplyAppleMatch(Episode episode, RedditPodcastPoster.PodcastServices.Apple.Models.ResolvedAppleItem match)
    {
        EpisodeServicePresence.TryFillMissing(episode, ServiceKeys.Apple, match.Url, match.Image);
        if (EpisodeServicePresence.AppleEpisodeId(episode) is null && match.EpisodeId is > 0)
        {
            EpisodeServicePresence.SetAppleIdentity(episode, match.EpisodeId);
        }
    }

    private static void ApplyYouTubeMatch(Episode episode, RedditPodcastPoster.PodcastServices.YouTube.Models.ResolvedYouTubeItem match)
    {
        EpisodeServicePresence.TryFillMissing(episode, ServiceKeys.YouTube, match.Url, match.Image);
        if (string.IsNullOrWhiteSpace(EpisodeServicePresence.YouTubeEpisodeId(episode)))
        {
            EpisodeServicePresence.SetYouTubeIdentity(episode, match.EpisodeId);
        }
    }

    private static void ApplySpotifyMatch(Episode episode, RedditPodcastPoster.PodcastServices.Spotify.Models.ResolvedSpotifyItem match)
    {
        EpisodeServicePresence.TryFillMissing(episode, ServiceKeys.Spotify, match.Url, match.Image);
        if (string.IsNullOrWhiteSpace(EpisodeServicePresence.SpotifyEpisodeId(episode)))
        {
            EpisodeServicePresence.SetSpotifyIdentity(episode, match.EpisodeId);
        }
    }
}
