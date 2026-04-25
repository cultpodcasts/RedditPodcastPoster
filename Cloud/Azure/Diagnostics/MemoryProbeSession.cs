using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Azure.Diagnostics;

public sealed class MemoryProbeSession : IMemoryProbeScope
{
    private readonly ILogger _logger;
    private readonly string _functionName;
    private readonly string _invocationId;
    private readonly Stopwatch _stopwatch;
    private readonly MemoryProbeSnapshot _start;

    public MemoryProbeSession(ILogger logger, string functionName, string invocationId)
    {
        _logger = logger;
        _functionName = functionName;
        _invocationId = invocationId;
        _stopwatch = Stopwatch.StartNew();
        _start = MemoryProbeSnapshot.Capture();

        _logger.LogWarning(
            "MemoryProbe.Start function-name='{FunctionName}' invocation-id='{InvocationId}' phase='start' managed-bytes='{ManagedBytes}' heap-bytes='{HeapSizeBytes}' working-set-bytes='{WorkingSetBytes}' private-bytes='{PrivateBytes}'.",
            _functionName,
            _invocationId,
            _start.ManagedBytes,
            _start.HeapSizeBytes,
            _start.WorkingSetBytes,
            _start.PrivateBytes);
    }

    public void End()
    {
        End(true);
    }

    public void End(bool success, string? errorType = null)
    {
        _stopwatch.Stop();
        var elapsedMs = _stopwatch.ElapsedMilliseconds;

        var memoryEnd = MemoryProbeSnapshot.Capture();

        if (string.IsNullOrWhiteSpace(errorType))
        {
            _logger.LogWarning(
                "MemoryProbe.Complete function-name='{FunctionName}' invocation-id='{InvocationId}' phase='complete' success='{Success}' elapsed-ms='{ElapsedMs}' managed-bytes='{ManagedBytes}' heap-bytes='{HeapSizeBytes}' working-set-bytes='{WorkingSetBytes}' private-bytes='{PrivateBytes}' managed-delta-bytes='{ManagedDeltaBytes}' heap-delta-bytes='{HeapDeltaBytes}' working-set-delta-bytes='{WorkingSetDeltaBytes}' private-delta-bytes='{PrivateDeltaBytes}'.",
                _functionName,
                _invocationId,
                success,
                elapsedMs,
                memoryEnd.ManagedBytes,
                memoryEnd.HeapSizeBytes,
                memoryEnd.WorkingSetBytes,
                memoryEnd.PrivateBytes,
                memoryEnd.ManagedBytes - _start.ManagedBytes,
                memoryEnd.HeapSizeBytes - _start.HeapSizeBytes,
                memoryEnd.WorkingSetBytes - _start.WorkingSetBytes,
                memoryEnd.PrivateBytes - _start.PrivateBytes);
            return;
        }

        _logger.LogWarning(
            "MemoryProbe.Complete function-name='{FunctionName}' invocation-id='{InvocationId}' phase='complete' success='{Success}' elapsed-ms='{ElapsedMs}' managed-bytes='{ManagedBytes}' heap-bytes='{HeapSizeBytes}' working-set-bytes='{WorkingSetBytes}' private-bytes='{PrivateBytes}' managed-delta-bytes='{ManagedDeltaBytes}' heap-delta-bytes='{HeapDeltaBytes}' working-set-delta-bytes='{WorkingSetDeltaBytes}' private-delta-bytes='{PrivateDeltaBytes}' error-type='{ErrorType}'.",
            _functionName,
            _invocationId,
            success,
            elapsedMs,
            memoryEnd.ManagedBytes,
            memoryEnd.HeapSizeBytes,
            memoryEnd.WorkingSetBytes,
            memoryEnd.PrivateBytes,
            memoryEnd.ManagedBytes - _start.ManagedBytes,
            memoryEnd.HeapSizeBytes - _start.HeapSizeBytes,
            memoryEnd.WorkingSetBytes - _start.WorkingSetBytes,
            memoryEnd.PrivateBytes - _start.PrivateBytes,
            errorType);
    }
}

internal sealed class NoOpMemoryProbeScope : IMemoryProbeScope
{
    public static readonly NoOpMemoryProbeScope Instance = new();

    public void End()
    {
    }

    public void End(bool success, string? errorType = null)
    {
    }
}
