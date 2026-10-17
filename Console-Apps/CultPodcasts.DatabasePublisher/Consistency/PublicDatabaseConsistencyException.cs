namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// The data cannot be published without making the public repo inconsistent. Derives from
/// <see cref="InvalidOperationException"/> like the checks it extends.
/// </summary>
public sealed class PublicDatabaseConsistencyException(IReadOnlyList<string> problems)
    : InvalidOperationException(
        $"Public database consistency check failed with {problems.Count} problem(s):{Environment.NewLine}" +
        string.Join(Environment.NewLine, problems.Select(p => $" - {p}")))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}
