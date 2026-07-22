using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastGetService
{
    Task<PodcastGetResult> GetAsync(PodcastGetRequest request, CancellationToken cancellationToken);
}
