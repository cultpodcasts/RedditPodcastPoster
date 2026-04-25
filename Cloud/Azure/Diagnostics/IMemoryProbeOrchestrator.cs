namespace Azure.Diagnostics;

public interface IMemoryProbeOrchestrator
{
    IMemoryProbeScope Start(string functionName);
}

public interface IMemoryProbeScope
{
    void End();
    void End(bool success, string? errorType = null);
}
