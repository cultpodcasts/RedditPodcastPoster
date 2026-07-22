using Api.Dtos;
using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastUpdateService
{
    Task<PodcastUpdateResult> UpdateAsync(
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        CancellationToken cancellationToken);
}
