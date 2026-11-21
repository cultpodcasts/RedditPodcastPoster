namespace Api.Contracts;

/// <summary>
/// Marks a DTO or enum in <c>Api.Dtos</c> as a root of the generated TypeScript contract package.
/// Referenced types in <c>Api.Dtos</c> are included. A type outside that namespace fails the build.
/// To export every public type in <c>Api.Dtos</c>, use <see cref="ExportContractNamespaceAttribute"/>
/// on the assembly instead of marking each type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum, Inherited = false)]
public sealed class ExportContractAttribute : Attribute;
