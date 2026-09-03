namespace RedditPodcastPoster.UrlSubmission.Models;

public class SubmitPodcastNotFoundException(Guid podcastId)
    : Exception($"No podcast exists for id '{podcastId}'.")
{
    public Guid PodcastId { get; } = podcastId;
}
