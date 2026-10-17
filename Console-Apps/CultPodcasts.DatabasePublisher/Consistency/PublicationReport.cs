namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>What a run wrote and, when a baseline was supplied, how that differs from the previous publish.</summary>
public sealed record PublicationReport(IReadOnlyList<PublishedItem> Published, BaselineComparison? Comparison);
