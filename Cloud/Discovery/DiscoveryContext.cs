namespace Discovery;

public record DiscoveryContext(
    Guid DiscoveryOperationId,
    bool? DuplicateDiscoveryOperation = null,
    bool? Success = null
    )
{
    public override string ToString()
    {
        var discoveryOperationId = $"indexer-operation-id: '{DiscoveryOperationId}'";

        var duplicateDiscoveryOperation = DuplicateDiscoveryOperation.HasValue
            ? $"duplicate-discovery-operation: '{DuplicateDiscoveryOperation}'"
            : string.Empty;

        var success = Success.HasValue
            ? $"success: '{Success}'"
            : string.Empty;


        return
            $"{nameof(DiscoveryContext)} Indexer-options {string.Join(", ", new[] { success, discoveryOperationId, duplicateDiscoveryOperation }.Where(x => !string.IsNullOrWhiteSpace(x)))}.";
    }

}