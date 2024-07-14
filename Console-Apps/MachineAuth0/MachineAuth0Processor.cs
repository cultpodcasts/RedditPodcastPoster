using Microsoft.Extensions.Logging;

namespace MachineAuth0;

public class MachineAuth0Processor(IApiClient apiClient, ILogger<MachineAuth0Processor> logger)
{
    public async Task Run()
    {
        await apiClient.Test();
    }
}