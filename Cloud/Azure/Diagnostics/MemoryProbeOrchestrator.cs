using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Azure.Diagnostics;

public class MemoryProbeOrchestrator(
    IOptions<MemoryProbeOptions> memoryProbeOptions,
    ILoggerFactory loggerFactory)
    : IMemoryProbeOrchestrator
{
    private readonly MemoryProbeOptions _memoryProbeOptions = memoryProbeOptions.Value;

    public IMemoryProbeScope Start(string functionName)
    {
        if (!_memoryProbeOptions.Enabled)
        {
            return NoOpMemoryProbeScope.Instance;
        }

        var logger = loggerFactory.CreateLogger(functionName);
        var invocationId = Guid.NewGuid().ToString("N");
        return new MemoryProbeSession(logger, functionName, invocationId);
    }
}
