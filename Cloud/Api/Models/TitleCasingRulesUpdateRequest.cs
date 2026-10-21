namespace Api.Models;

public class KnownTermUpdate
{
    public required string Literal { get; init; }

    public required string Pattern { get; init; }

    public string? Options { get; init; }
}
