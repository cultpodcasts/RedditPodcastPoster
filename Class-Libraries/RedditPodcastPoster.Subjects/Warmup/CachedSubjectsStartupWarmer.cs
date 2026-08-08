using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Subjects.Providers;

namespace RedditPodcastPoster.Subjects.Warmup;

public sealed class CachedSubjectsStartupWarmer(ICachedSubjectProvider subjectsProvider) : IStartupWarmer
{
    public string Name => nameof(ICachedSubjectProvider);

    public async Task WarmAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in subjectsProvider.GetAll().WithCancellation(cancellationToken))
        {
        }
    }
}
