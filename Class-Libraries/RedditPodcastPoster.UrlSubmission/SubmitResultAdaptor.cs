using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public class SubmitResultAdaptor : ISubmitResultAdaptor
{
    public DiscoverySubmitResultState ToDiscoverySubmitResultState(SubmitResult submitResult)
    {
        DiscoverySubmitResultState state;
        if (submitResult is
            {
                PodcastResult: SubmitResultState.Created,
                EpisodeResult: SubmitResultState.Created
            })
        {
            state = DiscoverySubmitResultState.CreatedPodcastAndEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.Enriched,
                     EpisodeResult: SubmitResultState.Created
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcastAndCreatedEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.Enriched,
                     EpisodeResult: SubmitResultState.None
                     or SubmitResultState.EpisodeAlreadyExists
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcast;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.Enriched,
                     EpisodeResult: SubmitResultState.Enriched
                 })
        {
            state = DiscoverySubmitResultState.EnrichedPodcastAndEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.None,
                     EpisodeResult: SubmitResultState.Created
                 })
        {
            state = DiscoverySubmitResultState.CreatedEpisode;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.None,
                     EpisodeResult: SubmitResultState.EpisodeAlreadyExists
                 })
        {
            state = DiscoverySubmitResultState.EpisodeAlreadyExists;
        }
        else if (submitResult is
                 {
                     PodcastResult: SubmitResultState.None,
                     EpisodeResult: SubmitResultState.Enriched
                 })
        {
            state = DiscoverySubmitResultState.EnrichedEpisode;
        }
        else
        {
            throw new ArgumentException(
                $"Unknown state: podcast-result: '{submitResult.PodcastResult.ToString()}', episode-result '{submitResult.EpisodeResult.ToString()}'.");
        }

        return state;
    }
}