using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
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
    ILogger<EnrichPodcastEpisodesProcessor> logger)
{
    public async Task Run(EnrichPodcastEpisodesRequest request)
    {
        IndexingContext indexingContext;
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

        var podcast = await podcastsRepository.GetPodcast(request.PodcastId);
        if (podcast == null)
        {
            throw new ArgumentException($"No podcast found with id '{request.PodcastId}'.");
        }

        IEnumerable<Episode> episodes = podcast.Episodes;
        if (request.ReleasedSince.HasValue)
        {
            episodes = episodes.Where(x => x.Release >= indexingContext.ReleasedSince);
        }

        var updated = false;
        foreach (var episode in episodes)
        {
            var criteria = new PodcastServiceSearchCriteria(podcast.Name, string.Empty, podcast.Publisher,
                episode.Title, episode.Description, episode.Release, episode.Length);
            if (podcast.AppleId != null && (episode.AppleId == null || episode.Urls.Apple == null))
            {
                var match = await appleUrlCategoriser.Resolve(criteria, podcast, indexingContext);
                if (match != null)
                {
                    episode.Urls.Apple ??= match.Url;
                    episode.AppleId ??= match.EpisodeId;
                    logger.LogInformation($"Enriched from apple: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
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
                                logger.LogInformation(
                                    $"Enriched from apple: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                                updated = true;
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
                            $"Enriched from youtube-url: '{episode.Urls.YouTube}', youtube-id: '{episode.YouTubeId}'.");
                    }
                }
                else if (episode.Urls.YouTube == null && !string.IsNullOrWhiteSpace(episode.YouTubeId))
                {
                    episode.Urls.YouTube = SearchResultExtensions.ToYouTubeUrl(episode.YouTubeId);
                    logger.LogInformation(
                        $"Enriched from youtube-id: '{episode.YouTubeId}', Url: '{episode.Urls.YouTube}'.");
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

                        logger.LogInformation(
                            $"Enriched episode with episode-id '{episode.Id}' from youtube: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                        updated = true;
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

                    logger.LogInformation($"Enriched from spotify: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                    updated = true;
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
                                logger.LogInformation(
                                    $"Enriched from spotify: Id: '{match.EpisodeId}', Url: '{match.Url}'.");
                                updated = true;
                            }
                        }
                    }
                }
            }
        }

        if (updated)
        {
            podcast.Episodes = podcast.Episodes.OrderByDescending(x => x.Release).ToList();
            await podcastsRepository.Save(podcast);
        }
    }
}