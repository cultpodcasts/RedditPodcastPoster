using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

namespace FilmSearchPusher;

internal static class FilmSearchPusherCommand
{
    public static FilmSearchPushRequest ToPushRequest(FilmSearchPusherRequest request) =>
        new(request.Full, request.DryRun, request.BatchSize, request.Id);

    /// <summary>0 when every push and delete succeeded (or a dry run planned cleanly); 1 otherwise, so a scheduler sees the failure.</summary>
    public static int ExitCode(FilmSearchPushResult result) => result.Succeeded ? 0 : 1;
}
