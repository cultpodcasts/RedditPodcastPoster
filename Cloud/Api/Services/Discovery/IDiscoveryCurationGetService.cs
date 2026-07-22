using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoveryCurationGetService
{
    Task<DiscoveryCurationGetResult> GetAsync(bool includeHidden, CancellationToken cancellationToken);
}
