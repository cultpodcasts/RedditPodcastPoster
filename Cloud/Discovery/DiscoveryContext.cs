namespace Discovery;

public record DiscoveryContext(Guid DiscoveryOperationId)
{
    public override string ToString()
    {
        var discoveryOperationId = $"indexer-operation-id: '{DiscoveryOperationId}'";

        return
            $"{nameof(DiscoveryContext)} Indexer-options {string.Join(", ", new[] {discoveryOperationId}.Where(x => !string.IsNullOrWhiteSpace(x)))}.";
    }

    public DateTime Completed { get; set; }
    public DateTime DiscoveryBegan { get; set; }
}