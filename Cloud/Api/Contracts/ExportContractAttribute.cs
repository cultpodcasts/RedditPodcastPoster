namespace Api.Contracts;

/// <summary>
/// Marks a DTO or enum as a root of the generated TypeScript contract package.
/// Referenced DTOs under <see cref="ContractExportRules.FollowNamespacePrefixes"/> and every
/// referenced enum are included automatically. To export every public type in a namespace,
/// use <see cref="ExportContractNamespaceAttribute"/> on the assembly instead of marking each type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false)]
public sealed class ExportContractAttribute : Attribute;
