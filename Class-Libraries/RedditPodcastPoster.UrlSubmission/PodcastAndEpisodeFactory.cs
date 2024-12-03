using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public class PodcastAndEpisodeFactory(
    IEpisodeFactory episodeFactory,
    IPodcastFactory podcastFactory,
    ISubjectEnricher subjectEnricher,
    ILogger<PodcastAndEpisodeFactory> logger
) : IPodcastAndEpisodeFactory
{
    public async Task<CreatePodcastWithEpisodeResponse> CreatePodcastWithEpisode(
        CategorisedItem categorisedItem)
    {
        string showName;
        string publisher;
        switch (categorisedItem.Authority)
        {
            case Service.Apple:
                showName = categorisedItem.ResolvedAppleItem!.ShowName;
                publisher = categorisedItem.ResolvedAppleItem.Publisher;
                break;
            case Service.Spotify:
                showName = categorisedItem.ResolvedSpotifyItem!.ShowName;
                publisher = categorisedItem.ResolvedSpotifyItem.Publisher;
                break;
            case Service.YouTube:
                showName = categorisedItem.ResolvedYouTubeItem!.ShowName;
                publisher = categorisedItem.ResolvedYouTubeItem.Publisher;
                break;
            case Service.Other:
                showName = categorisedItem.ResolvedNonPodcastServiceItem!.Title!;
                publisher = categorisedItem.ResolvedNonPodcastServiceItem!.Publisher!;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var newPodcast = await podcastFactory.Create(showName);
        newPodcast.Publisher = publisher;
        newPodcast.SpotifyId = categorisedItem.ResolvedSpotifyItem?.ShowId ?? string.Empty;
        newPodcast.AppleId = categorisedItem.ResolvedAppleItem?.ShowId;
        newPodcast.YouTubeChannelId = categorisedItem.ResolvedYouTubeItem?.ShowId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(newPodcast.YouTubeChannelId))
        {
            newPodcast.YouTubePublicationOffset = Constants.DefaultMatchingPodcastYouTubePublishingDelay.Ticks;
        }

        var episode = episodeFactory.CreateEpisode(categorisedItem);
        var subjectsResult = await subjectEnricher.EnrichSubjects(episode);
        newPodcast.Episodes.Add(episode);
        logger.LogInformation($"Created podcast with name '{showName}' with id '{newPodcast.Id}'.");

        var submitEpisodeDetails = new SubmitEpisodeDetails(
            episode.Urls.Spotify != null,
            episode.Urls.Apple != null,
            episode.Urls.YouTube != null,
            subjectsResult.Additions,
            episode.Urls.BBC != null,
            episode.Urls.InternetArchive != null);
        return new CreatePodcastWithEpisodeResponse(newPodcast, episode, submitEpisodeDetails);
    }
}