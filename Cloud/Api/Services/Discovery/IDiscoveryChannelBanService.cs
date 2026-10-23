using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoveryChannelBanService
{
    Task<DiscoveryChannelBanResult> BanAsync(Guid resultId, CancellationToken cancellationToken);
}
