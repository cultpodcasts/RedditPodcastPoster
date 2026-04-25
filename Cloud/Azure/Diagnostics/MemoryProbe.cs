using System.Diagnostics;

namespace Azure.Diagnostics;

public class MemoryProbeOptions
{
    public bool Enabled { get; set; }
}

public readonly record struct MemoryProbeSnapshot(
    long ManagedBytes,
    long HeapSizeBytes,
    long WorkingSetBytes,
    long PrivateBytes)
{
    public static MemoryProbeSnapshot Capture()
    {
        var gcMemoryInfo = GC.GetGCMemoryInfo();
        using var process = Process.GetCurrentProcess();
        return new MemoryProbeSnapshot(
            GC.GetTotalMemory(false),
            gcMemoryInfo.HeapSizeBytes,
            process.WorkingSet64,
            process.PrivateMemorySize64);
    }
}
