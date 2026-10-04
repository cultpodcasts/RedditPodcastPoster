namespace Azure;

/// <summary>
/// Azure Monitor OTel export choices that must stay independent of the trace sampler.
/// </summary>
public static class AzureMonitorExporterPolicy
{
    /// <summary>
    /// When true, AppTraces/AppExceptions ride the trace sampler (default 25%).
    /// A 5xx Isolated request can then appear in AppRequests with no failure log.
    /// 2026-10-04 POST SubmitUrl 500 had that gap. Keep false so Error logs always export.
    /// </summary>
    public const bool EnableTraceBasedLogsSampler = false;
}
