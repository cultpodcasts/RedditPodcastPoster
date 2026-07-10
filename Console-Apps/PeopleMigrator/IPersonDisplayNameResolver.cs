namespace PeopleMigrator;

public interface IPersonDisplayNameResolver
{
    Task<DisplayNameResolution> ResolveDisplayNameAsync(
        string? twitterHandle,
        string? blueskyHandle,
        CancellationToken cancellationToken);
}
