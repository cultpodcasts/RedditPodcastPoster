using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastIndexService
{
    Task<PodcastIndexResult> IndexAsync(string podcastName, CancellationToken cancellationToken);
}
