using Discovery.Models;

namespace Discovery.Services;

/// <summary>
/// Thrown when Discovery cannot resolve a Dynamic lookback window because there is no prior
/// successful <c>discoveryBegan</c> watermark, or Cosmos lookup failed.
/// Cloud Discovery fails closed — first successful run must be via CLI (<c>Console-Apps/Discover</c>).
/// </summary>
public class DiscoveryLookbackUnavailableException : Exception
{
    public DiscoveryLookbackUnavailableException(string message) : base(message)
    {
    }

    public DiscoveryLookbackUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
