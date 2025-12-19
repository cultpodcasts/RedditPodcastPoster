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
    IPodcastRepository podcastsRepository,
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
                    x.Name.Contains(request.PodcastName, StringComparison.InvariantCultureIgnoreCase),
                x => x.Id).ToListAsync();
            if (!podcastIds.Any())
            {
                throw new InvalidOperationException($"No podcast matching '{request.PodcastName}' could be found.");
            }

            if (podcastIds.Count() > 1)
            {
                throw new InvalidOperationException($"Multiple podcasts matching '{request.PodcastName}' were found. Ids: {string.Join(", ", podcastIds)}.");
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

        IEnumerable<Episode> episodes = podcast.Episodes;
        if (request.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince);
        }

        var podcastUpdated = false;
        foreach (var episode in episodes)
        {
            var episodeUpdated = false;
            var criteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty, podcast.Publisher,
                episode.Title, episode.Description, episode.Release, episode.Length);

            if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                !string.IsNullOrWhiteSpace(podcast.SpotifyId) &&
                !string.IsNullOrWhiteSpace(episode.SpotifyId) &&
                episode.AppleId == null)
            {
                var spotifyEpisode =
                    await spotifyEpisodeResolver.FindEpisode(FindSpotifyEpisodeRequestFactory.Create(podcast, episode),
                        indexingContext);
                if (spotifyEpisode?.FullEpisode != null &&
                    spotifyEpisode.FullEpisode.Name.Trim() != episode.Title.Trim())
                {
                    criteria.SpotifyTitle = spotifyEpisode.FullEpisode.Name.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(podcast.YouTubeChannelId) &&
                podcast.AppleId != null &&
                episode.AppleId != null &&
                string.IsNullOrWhiteSpace(episode.SpotifyId))
            {
                var appleEpisode =
                    await appleEpisodeResolver.FindEpisode(FindAppleEpisodeRequestFactory.Create(podcast, episode),
                        indexingContext);
                if (appleEpisode != null && appleEpisode.Title.Trim() != episode.Title.Trim())
                {
                    criteria.AppleTitle = appleEpisode.Title.Trim();
                }
            }

            if (podcast.AppleId != null && (episode.AppleId == null || episode.Urls.Apple == null))
            {
                var match = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Apple ??= match.Url;
                    episode.AppleId ??= match.EpisodeId;
                    var appleImage = match.Image;
                    if (appleImage != null)
                    {
                        episode.Images ??= new EpisodeImages();
                        episode.Images.Apple = appleImage;
                    }

                    logger.LogInformation("Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId, match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((!string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var spotifyEpisode =
                            await spotifyEpisodeResolver.FindEpisode(
                                FindSpotifyEpisodeRequestFactory.Create(podcast, episode), indexingContext);
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
                                episode.Urls.Apple ??= match.Url;
                                episode.AppleId ??= match.EpisodeId;
                                var appleImage = match.Image;
                                if (appleImage != null)
                                {
                                    episode.Images ??= new EpisodeImages();
                                    episode.Images.Apple = appleImage;
                                }

                                logger.LogInformation(
                                    "Enriched from apple: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId, match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (podcast.YouTubeChannelId != null &&
                (string.IsNullOrWhiteSpace(episode.YouTubeId) || episode.Urls.YouTube == null))
            {
                if (string.IsNullOrWhiteSpace(episode.YouTubeId) && episode.Urls.YouTube != null)
                {
                    var youTubeId = YouTubeIdResolver.Extract(episode.Urls.YouTube);
                    if (!string.IsNullOrWhiteSpace(youTubeId))
                    {
                        episode.YouTubeId = youTubeId;
                        logger.LogInformation(
                            "Enriched from youtube-url: '{UrlsYouTube}', youtube-id: '{EpisodeYouTubeId}'.", episode.Urls.YouTube, episode.YouTubeId);
                    }
                }
                else if (episode.Urls.YouTube == null && !string.IsNullOrWhiteSpace(episode.YouTubeId))
                {
                    episode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(episode.YouTubeId);
                    logger.LogInformation(
                        "Enriched from youtube-id: '{EpisodeYouTubeId}', Url: '{UrlsYouTube}'.", episode.YouTubeId, episode.Urls.YouTube);
                }
                else
                {
                    var match = await youTubeUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                    if (match != null)
                    {
                        episode.Urls.YouTube ??= match.Url;
                        if (string.IsNullOrWhiteSpace(episode.YouTubeId))
                        {
                            episode.YouTubeId = match.EpisodeId;
                        }

                        var youTubeImage = match.Image;
                        if (youTubeImage != null)
                        {
                            episode.Images ??= new EpisodeImages();
                            episode.Images.YouTube = youTubeImage;
                        }

                        logger.LogInformation(
                            "Enriched episode with episode-id '{EpisodeId}' from youtube: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", episode.Id, match.EpisodeId, match.Url);
                        episodeUpdated = true;
                    }
                }
            }

            if (podcast.SpotifyId != null &&
                (string.IsNullOrWhiteSpace(episode.SpotifyId) || episode.Urls.Spotify == null))
            {
                var match = await spotifyUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Spotify ??= match.Url;
                    if (string.IsNullOrWhiteSpace(episode.SpotifyId))
                    {
                        episode.SpotifyId = match.EpisodeId;
                    }

                    var spotifyImage = match.Image;
                    if (spotifyImage != null)
                    {
                        episode.Images ??= new EpisodeImages();
                        episode.Images.Spotify = spotifyImage;
                    }

                    logger.LogInformation("Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId, match.Url);
                    episodeUpdated = true;
                }
                else
                {
                    if ((episode.AppleId != null || episode.Urls.Apple != null) &&
                        podcast.ReleaseAuthority == Service.YouTube)
                    {
                        var appleEpisode =
                            await appleEpisodeResolver.FindEpisode(
                                FindAppleEpisodeRequestFactory.Create(podcast, episode), indexingContext);
                        if (appleEpisode != null)
                        {
                            var refinedCriteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty,
                                podcast.Publisher, appleEpisode.Title, appleEpisode.Description, appleEpisode.Release,
                                appleEpisode.Duration);
                            match = await spotifyUrlCategoriser.Resolve(refinedCriteria, podcast, indexingContext);
                            if (match != null)
                            {
                                episode.Urls.Spotify ??= match.Url;
                                episode.SpotifyId = match.EpisodeId;
                                var spotifyImage = match.Image;
                                if (spotifyImage != null)
                                {
                                    episode.Images ??= new EpisodeImages();
                                    episode.Images.Spotify = spotifyImage;
                                }

                                logger.LogInformation(
                                    "Enriched from spotify: Id: '{MatchEpisodeId}', Url: '{MatchUrl}'.", match.EpisodeId, match.Url);
                                episodeUpdated = true;
                            }
                        }
                    }
                }
            }

            if (episodeUpdated)
            {
                updatedEpisodeIds.Add(episode.Id);
            }

            podcastUpdated |= episodeUpdated;
        }

        if (podcastUpdated)
        {
            podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();
            await podcastsRepository.Save(podcast);
            await episodeSearchIndexerService.IndexEpisodes(updatedEpisodeIds, CancellationToken.None);
        }
    }
}