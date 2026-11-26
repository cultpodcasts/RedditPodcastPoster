namespace ContractExporter;

public enum ContractShape
{
    Object,
    StringEnum,
    NumberEnum
}

public enum WireKind
{
    String,
    Integer,
    Number,
    Boolean,
    Uuid,
    IsoDateTime,
    OffsetDateTime,
    TimeSpan,
    Uri,
    Array,
    Dictionary,
    Named
}

/// <summary>A JSON type. <see cref="Nullable"/> means the value may be JSON null.</summary>
public sealed record WireType(
    WireKind Kind,
    bool Nullable,
    string? Name = null,
    WireType? Item = null,
    WireType? DictionaryValue = null)
{
    public WireType AsNullable() => this with { Nullable = true };
}

public sealed record ContractProperty(string JsonName, int Order, int Token, WireType Type, bool Optional);

public sealed record EnumMember(string Name, long Number, string WireText);

public sealed class ContractType
{
    public required string Name { get; init; }
    public required string ClrFullName { get; init; }
    public required ContractShape Shape { get; set; }
    public List<ContractProperty> Properties { get; } = [];
    public List<EnumMember> Members { get; } = [];
}

public sealed class ContractDocument
{
    public required IReadOnlyList<ContractType> Types { get; init; }
}
