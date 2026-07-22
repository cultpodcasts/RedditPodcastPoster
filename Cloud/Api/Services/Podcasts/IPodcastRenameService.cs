using Api.Models;

namespace Api.Services.Podcasts;

public interface IPodcastRenameService
{
    Task<PodcastRenameResult> RenameAsync(PodcastRenameRequest change, CancellationToken cancellationToken);
}
