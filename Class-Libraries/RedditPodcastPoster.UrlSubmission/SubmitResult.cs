using static RedditPodcastPoster.UrlSubmission.SubmitResult;

namespace RedditPodcastPoster.UrlSubmission;

public record SubmitResult(
    SubmitResultState EpisodeResult,
    SubmitResultState PodcastResult,
    SubmitEpisodeDetails? SubmitEpisodeDetails = null,
    Guid? EpisodeId = null
)
{
    public enum SubmitResultState
    {
        None = 0,
        Created,
        Enriched,
        PodcastRemoved,
        EpisodeAlreadyExists
    }

    public override string ToString()
    {
        var results = new List<string>
        {
            $"Podcast-Result: '{PodcastResult.ToString()}'",
            $"episode-Result: '{EpisodeResult.ToString()}'"
        };

        if (EpisodeId != null)
        {
            results.Add($"episode-id: '{EpisodeId}'");
        }

        if (SubmitEpisodeDetails != null)
        {
            results.Add(
                $"spotify: {SubmitEpisodeDetails.Spotify}, apple: {SubmitEpisodeDetails.Apple}, youtube: {SubmitEpisodeDetails.YouTube}");
            if (SubmitEpisodeDetails.Subjects != null)
            {
                results.Add($"subjects: '{string.Join("', '", SubmitEpisodeDetails.Subjects)}'");
            }
        }

        return string.Join(", ", results);
    }
}