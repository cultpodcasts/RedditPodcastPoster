namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>
/// The plan of a run and whether it was carried out (it is not when the plan is blocked); the path of the
/// migration manifest when one was written.
/// </summary>
public sealed record PublicationReport(PublicationPlan Plan, bool Applied, string? MigrationsFile = null);
