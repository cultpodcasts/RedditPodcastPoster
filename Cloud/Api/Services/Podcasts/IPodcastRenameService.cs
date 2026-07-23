using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastRenameService
{
    Task<PodcastRenameResult> RenameAsync(PodcastRenameCommand change, CancellationToken cancellationToken);
}
