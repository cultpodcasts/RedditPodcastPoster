using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace RedditPodcastPoster.PlaySuisse;

public static class PlaySuisseStreamingService
{
    public const string Key = StreamingServiceKeys.PlaySuisse;

    public static readonly IStreamingServiceRegistration Registration = new StreamingServiceRegistration(
        Key,
        "Play Suisse",
        "play-suisse",
        true,
        ["playsuisse.ch"]);
}
