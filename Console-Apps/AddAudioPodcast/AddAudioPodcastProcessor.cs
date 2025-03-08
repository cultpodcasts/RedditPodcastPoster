using System.Text.RegularExpressions;
using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Enrichers;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Models;
using SpotifyAPI.Web;

namespace AddAudioPodcast;

public class AddAudioPodcastProcessor(
    IPodcastRepository podcastRepository,
    ISpotifyClient spotifyClient,
    IPodcastFactory podcastFactory,
    IApplePodcastEnricher applePodcastEnricher,
    ISpotifyPodcastEnricher spotifyPodcastEnricher,
    IPodcastUpdater podcastUpdater,
    iTunesSearchManager iTunesSearchManager,
    ISubjectEnricher subjectEnricher,
    ILogger<AddAudioPodcastProcessor> logger)
{
    private readonly IndexingContext _indexingContext = new(SkipYouTubeUrlResolving: true);

    public async Task Create(AddAudioPodcastRequest request)
    {
        var indexingContext = new IndexingContext {SkipPodcastDiscovery = false};
        Podcast? podcast = null;
        if (!request.AppleReleaseAuthority)
        {
            var matchingPodcasts = await podcastRepository
                .GetAllBy(x => x.SpotifyId == request.PodcastId, x => new {x.Id}).ToListAsync();
            if (matchingPodcasts.Count() > 0)
            {
                throw new InvalidOperationException(
                    $"Found podcasts with spotify-id '{request.PodcastId}'. Podcast-ids: {string.Join(",", matchingPodcasts.Select(x => $"'{x.Id}'"))}.");
            }

            try
            {
                podcast = await GetSpotifyPodcast(request, indexingContext);
            }
            catch (APITooManyRequestsException e)
            {
                logger.LogError(e, "{exception}. Retry-after: {retryAfter}.", e.GetType(), e.RetryAfter);
                throw;
            }
        }
        else
        {
            var appleId = long.Parse(request.PodcastId);
            var matchingPodcasts =
                await podcastRepository.GetAllBy(x => x.AppleId == appleId, x => new {x.Id}).ToListAsync();
            if (matchingPodcasts.Count() > 0)
            {
                throw new InvalidOperationException(
                    $"Found podcasts with apple-id '{request.PodcastId}'. Podcast-ids: {string.Join(",", matchingPodcasts.Select(x => $"'{x.Id}'"))}.");
            }

            podcast = await GetApplePodcast(request, indexingContext);
        }

        if (podcast != null)
        {
            var result = await podcastUpdater.Update(podcast, _indexingContext);

            if (!string.IsNullOrWhiteSpace(request.EpisodeTitleRegex))
            {
                var includeEpisodesRegex = new Regex(request.EpisodeTitleRegex,
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);

                podcast.IndexAllEpisodes = false;
                podcast.EpisodeIncludeTitleRegex = request.EpisodeTitleRegex;
                List<Episode> episodesToRemove = new();
                foreach (var episode in podcast.Episodes)
                {
                    var match = includeEpisodesRegex.Match(episode.Title);
                    if (!match.Success)
                    {
                        episodesToRemove.Add(episode);
                    }
#if DEBUG
                    else
                    {
                        var matchedTerm = match.Groups[0].Value;
                    }
#endif
                }

                foreach (var episode in episodesToRemove)
                {
                    podcast.Episodes.Remove(episode);
                    logger.LogInformation($"Removed episode '{episode.Title}' due to regex.");
                }
            }

            foreach (var episode in podcast.Episodes)
            {
                var results = await subjectEnricher.EnrichSubjects(episode,
                    new SubjectEnrichmentOptions(
                        podcast.IgnoredAssociatedSubjects,
                        podcast.IgnoredSubjects,
                        podcast.DefaultSubject,
                        podcast.DescriptionRegex));
            }

            await podcastRepository.Save(podcast);

            if (result.Success)
            {
                logger.LogInformation(result.ToString());
            }
            else
            {
                logger.LogError(result.ToString());
            }
        }
        else
        {
            var source = request.AppleReleaseAuthority ? "Apple" : "Spotify";
            logger.LogError($"Could not find podcast with id '{request.PodcastId}' using '{source}' as source.");
        }
    }

    private async Task<Podcast> GetSpotifyPodcast(AddAudioPodcastRequest request, IndexingContext indexingContext)
    {
        var spotifyPodcast =
            await spotifyClient.Shows.Get(request.PodcastId, new ShowRequest {Market = Market.CountryCode});
        logger.LogInformation("Retrieved spotify podcast");
        var podcast = await podcastRepository.GetBy(x => x.SpotifyId == request.PodcastId);
        if (podcast == null)
        {
            podcast = await podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();
            podcast.SpotifyMarket = request.SpotifyMarket;

            await applePodcastEnricher.AddId(podcast);
        }

        return podcast;
    }

    private async Task<Podcast?> GetApplePodcast(AddAudioPodcastRequest request, IndexingContext indexingContext)
    {
        var id = long.Parse(request.PodcastId);
        var applePodcast = (await iTunesSearchManager.GetPodcastById(id)).Podcasts.SingleOrDefault();
        logger.LogInformation("Retrieved apple podcast");

        if (applePodcast == null)
        {
            logger.LogError($"No apple-podcast found for apple-id '{id}'.");
            return null;
        }

        var podcast = await podcastRepository.GetBy(x => x.AppleId == id);
        if (podcast == null)
        {
            podcast = await podcastFactory.Create(applePodcast.Name);
            podcast.AppleId = applePodcast.Id;
            podcast.Bundles = false;
            podcast.Publisher = applePodcast.ArtistName.Trim();
            podcast.ReleaseAuthority = Service.Apple;

            await spotifyPodcastEnricher.AddIdAndUrls(podcast, indexingContext);
        }

        return podcast;
    }
}