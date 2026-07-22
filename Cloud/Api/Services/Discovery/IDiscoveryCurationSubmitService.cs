using Api.Dtos;
using Api.Models;

namespace Api.Services.Discovery;

public interface IDiscoveryCurationSubmitService
{
    Task<DiscoveryCurationSubmitResult> SubmitAsync(
        DiscoverySubmitRequest request,
        CancellationToken cancellationToken);
}
