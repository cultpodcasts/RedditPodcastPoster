namespace Api.Contracts;

/// <summary>
/// Exports every public concrete class, struct and enum in <paramref name="contractNamespace"/>
/// as a contract root. Nested namespaces are not included; add another attribute for those.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ExportContractNamespaceAttribute(string contractNamespace) : Attribute
{
    public string ContractNamespace { get; } = contractNamespace;
}
