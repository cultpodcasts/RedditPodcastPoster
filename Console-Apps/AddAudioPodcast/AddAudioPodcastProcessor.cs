using System.Text.RegularExpressions;
using iTunesSearch.Library;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.Subjects;
using SpotifyAPI.Web;

namespace AddAudioPodcast;

public class AddAudioPodcastProcessor(
    IPodcastRepository podcastRepository,
    ISpotifyClient spotifyClient,
    PodcastFactory podcastFactory,
    IApplePodcastEnricher applePodcastEnricher,
    ISpotifyPodcastEnricher spotifyPodcastEnricher,
    IPodcastUpdater podcastUpdater,
    iTunesSearchManager iTunesSearchManager,
    ISubjectEnricher subjectEnricher,
    ILogger<AddAudioPodcastProcessor> logger)
{
    private readonly IndexingContext _indexingContext = new(null, true);

    public async Task Create(AddAudioPodcastRequest request)
    {
        var indexingContext = new IndexingContext {SkipPodcastDiscovery = false};
        var existingPodcasts = await podcastRepository.GetAll().ToListAsync();
        Podcast? podcast = null;
        if (!request.AppleReleaseAuthority)
        {
            podcast = await GetSpotifyPodcast(request, indexingContext, existingPodcasts);
        }
        else
        {
            podcast = await GetApplePodcast(request, indexingContext, existingPodcasts);
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
                await subjectEnricher.EnrichSubjects(episode,
                    new SubjectEnrichmentOptions(podcast.IgnoredAssociatedSubjects, podcast.DefaultSubject));
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

    private async Task<Podcast> GetSpotifyPodcast(AddAudioPodcastRequest request, IndexingContext indexingContext,
        List<Podcast> existingPodcasts)
    {
        var spotifyPodcast =
            await spotifyClient.Shows.Get(request.PodcastId, new ShowRequest {Market = Market.CountryCode});

        var podcast = existingPodcasts.SingleOrDefault(x => x.SpotifyId == request.PodcastId);
        if (podcast == null)
        {
            podcast = podcastFactory.Create(spotifyPodcast.Name);
            podcast.SpotifyId = spotifyPodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = spotifyPodcast.Publisher.Trim();
            podcast.SpotifyMarket = request.SpotifyMarket;

            await applePodcastEnricher.AddId(podcast);
        }

        return podcast;
    }


    private async Task<Podcast?> GetApplePodcast(AddAudioPodcastRequest request, IndexingContext indexingContext,
        List<Podcast> existingPodcasts)
    {
        var id = long.Parse(request.PodcastId);
        var applePodcast = (await iTunesSearchManager.GetPodcastById(id)).Podcasts.SingleOrDefault();

        if (applePodcast == null)
        {
            logger.LogError($"No apple-podcast found for apple-id '{id}'.");
            return null;
        }

        var podcast = existingPodcasts.SingleOrDefault(x => x.AppleId == id);
        if (podcast == null)
        {
            podcast = podcastFactory.Create(applePodcast.Name);
            podcast.AppleId = applePodcast.Id;
            podcast.ModelType = ModelType.Podcast;
            podcast.Bundles = false;
            podcast.Publisher = applePodcast.ArtistName.Trim();
            podcast.ReleaseAuthority = Service.Apple;

            await spotifyPodcastEnricher.AddIdAndUrls(podcast, indexingContext);
        }

        return podcast;
    }
}