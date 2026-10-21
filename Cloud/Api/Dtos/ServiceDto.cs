namespace Api.Dtos;

/// <summary>
/// Streaming service. Names and values mirror the domain <c>Service</c> enum so string and
/// numeric JSON representations are unchanged.
/// </summary>
public enum ServiceDto
{
    Spotify = 1,
    Apple,
    YouTube,
    Other
}
