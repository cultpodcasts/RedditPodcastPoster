namespace RedditPodcastPoster.PodcastServices.YouTube.Quota;

public sealed class YouTubeIndexerKeySessionBootstrapper(
    YouTubeIndexerKeyRingSessionHolder sessionHolder,
    IYouTubeIndexerKeyStateService indexerKeyStateService)
{
    private Task? _loadTask;

    public Task EnsureLoadedAsync()
    {
        _loadTask ??= LoadAsync();
        return _loadTask;
    }

    private async Task LoadAsync()
    {
        sessionHolder.Value = await indexerKeyStateService.ResolveSessionStartAsync();
    }
}
