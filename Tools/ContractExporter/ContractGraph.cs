using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Contracts;

namespace ContractExporter;

/// <summary>
/// Walks <see cref="ExportContractAttribute"/> roots (and assembly namespace exports) and the DTO
/// closure, classifying each property the way the HTTP serializer writes it.
/// </summary>
public static class ContractGraph
{
    public static ContractDocument Build() => Build(Api.Serialization.ApiJson.CreateHttpSerializerOptions());

    public static ContractDocument Build(JsonSerializerOptions options)
    {
        var assembly = typeof(ExportContractAttribute).Assembly;
        var roots = new List<Type>();
        foreach (var type in GetLoadableTypes(assembly))
        {
            if (type.GetCustomAttribute<ExportContractAttribute>(inherit: false) is not null)
            {
                roots.Add(type);
            }
        }

        foreach (var ns in assembly.GetCustomAttributes<ExportContractNamespaceAttribute>())
        {
            foreach (var type in GetLoadableTypes(assembly))
            {
                if (type.Namespace == ns.ContractNamespace && IsNamespaceExportCandidate(type))
                {
                    roots.Add(type);
                }
            }
        }

        var builder = new Builder(options);
        foreach (var root in roots.Distinct())
        {
            if (root.Namespace is not "Api.Dtos" && root.Namespace?.StartsWith("Api.Dtos.", StringComparison.Ordinal) != true)
            {
                throw new NotSupportedException(
                    $"Export root {root.FullName} is outside Api.Dtos. Mark only Api.Dtos types with {nameof(ExportContractAttribute)}.");
            }

            builder.Include(root);
        }

        builder.ReconcileEnumStyles();
        return new ContractDocument { Types = builder.OrderedTypes() };
    }

    private static bool IsNamespaceExportCandidate(Type type) =>
        (type.IsPublic || type.IsNestedPublic)
        && !type.IsAbstract
        && !type.IsInterface
        && !type.IsGenericTypeDefinition
        && (type.IsClass || type.IsValueType || type.IsEnum)
        && type.GetCustomAttribute<System.Runtime.CompilerServices.CompilerGeneratedAttribute>() is null;

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }

    private sealed class Builder(JsonSerializerOptions options)
    {
        private readonly Dictionary<Type, ContractType> _types = new();
        private readonly NullabilityInfoContext _nullability = new();
        private readonly List<(Type EnumType, JsonConverterAttribute? Converter)> _enumUses = [];

        public void Include(Type type, PropertyInfo? via = null)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (IsScalar(type) || IsDictionary(type) || IsCollection(type, out _))
            {
                return;
            }

            if (_types.ContainsKey(type))
            {
                return;
            }

            if (type.IsGenericType)
            {
                throw new NotSupportedException(
                    $"Generic contract type {type.FullName} is not supported. Export a closed non-generic DTO.");
            }

            if (!CanFollow(type))
            {
                throw OutsideApiDtos(type, via);
            }

            if (type.IsEnum && type.GetCustomAttribute<FlagsAttribute>() is not null)
            {
                throw new NotSupportedException(
                    $"{type.FullName} is a flags enum. The contract exporter does not yet emit flag combinations.");
            }

            var contract = new ContractType
            {
                Name = TypeScriptName(type),
                ClrFullName = type.FullName ?? type.Name,
                Shape = type.IsEnum ? ContractShape.NumberEnum : ContractShape.Object
            };
            _types.Add(type, contract);

            if (type.IsEnum)
            {
                AddEnumMembers(type, contract);
                return;
            }

            var properties = JsonProperties(type).ToList();
            for (var i = 0; i < properties.Count; i++)
            {
                // Index is the reflection order System.Text.Json stable-sorts by JsonPropertyOrder.
                contract.Properties.Add(BuildProperty(properties[i], i));
            }

            contract.Properties.Sort(static (a, b) =>
            {
                var byOrder = a.Order.CompareTo(b.Order);
                return byOrder != 0 ? byOrder : a.Token.CompareTo(b.Token);
            });
        }

        private static IEnumerable<PropertyInfo> JsonProperties(Type type) =>
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0)
                .Where(p => p.GetMethod is { IsPublic: true })
                .Where(p => p.GetCustomAttribute<JsonExtensionDataAttribute>() is null)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });

        public void ReconcileEnumStyles()
        {
            var grouped = _enumUses.GroupBy(u => u.EnumType);
            foreach (var group in grouped)
            {
                var styles = group
                    .Select(u => ProbeEnumStyle(u.EnumType, u.Converter))
                    .Distinct()
                    .ToList();
                if (styles.Count != 1)
                {
                    throw new NotSupportedException(
                        $"{group.Key.FullName} is serialized both as a string and as a number on exported contracts. " +
                        "Give every property the same JsonStringEnumConverter (or none).");
                }

                var contract = _types[group.Key];
                var style = styles[0];
                contract.Shape = style == EnumWire.String ? ContractShape.StringEnum : ContractShape.NumberEnum;
                var converter = group.Select(u => u.Converter).FirstOrDefault(c => c is not null);
                // A unanimous string style that comes from a property converter must be probed with that converter.
                // When the type itself already serializes as that style, converter may be null and the default options apply.
                if (ProbeEnumStyle(group.Key, null) != style)
                {
                    converter ??= group.First().Converter;
                }
                else
                {
                    converter = null;
                }

                RewriteMemberWire(group.Key, contract, converter);
            }

            foreach (var (type, contract) in _types)
            {
                if (!type.IsEnum || _enumUses.Any(u => u.EnumType == type))
                {
                    continue;
                }

                var style = ProbeEnumStyle(type, null);
                contract.Shape = style == EnumWire.String ? ContractShape.StringEnum : ContractShape.NumberEnum;
                RewriteMemberWire(type, contract, null);
            }
        }

        public IReadOnlyList<ContractType> OrderedTypes() =>
            _types.Values
                .OrderBy(t => t.Shape == ContractShape.Object)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .ToList();

        private ContractProperty BuildProperty(PropertyInfo property, int sequence)
        {
            var info = _nullability.Create(property);
            var converter = property.GetCustomAttribute<JsonConverterAttribute>(inherit: true);
            var wire = BuildWire(property.PropertyType, info, converter, property);
            var (optional, nullable) = Nullability(property, info);
            if (nullable && !wire.Nullable)
            {
                wire = wire.AsNullable();
            }
            else if (!nullable && wire.Nullable)
            {
                wire = wire with { Nullable = false };
            }

            var order = property.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order ?? 0;
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                       ?? options.PropertyNamingPolicy?.ConvertName(property.Name)
                       ?? property.Name;
            return new ContractProperty(name, order, sequence, wire, optional);
        }

        private (bool Optional, bool Nullable) Nullability(PropertyInfo property, NullabilityInfo info)
        {
            var clrNullable = IsClrNullable(property.PropertyType, info);
            var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
            var whenNull = ignore is { Condition: JsonIgnoreCondition.WhenWritingNull }
                           || (ignore is null && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull);
            var whenDefault = ignore is { Condition: JsonIgnoreCondition.WhenWritingDefault }
                              || (ignore is null && options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingDefault);
            var explicitWhenWritingNull = ignore is { Condition: JsonIgnoreCondition.WhenWritingNull };

            bool optional;
            bool nullable;
            if (explicitWhenWritingNull || (whenNull && clrNullable))
            {
                // WhenWritingNull omits the key. The value is still legally null on input, so the
                // contract type is optional and nullable.
                optional = true;
                nullable = true;
            }
            else if (whenDefault)
            {
                optional = true;
                nullable = clrNullable;
            }
            else
            {
                optional = false;
                nullable = clrNullable;
            }

            if (property.GetCustomAttribute<JsonRequiredAttribute>() is not null)
            {
                optional = false;
            }

            return (optional, nullable);
        }

        private WireType BuildWire(Type type, NullabilityInfo? info, JsonConverterAttribute? converter, PropertyInfo? property)
        {
            var underlying = Nullable.GetUnderlyingType(type) ?? type;
            if (underlying.IsEnum)
            {
                Include(underlying, property);
                if (property is not null && (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == underlying)
                {
                    _enumUses.Add((underlying, converter));
                }

                return new WireType(WireKind.Named, Nullable.GetUnderlyingType(type) is not null, Name: TypeScriptName(underlying));
            }

            if (IsDictionary(underlying, out var key, out var value))
            {
                if (key != typeof(string))
                {
                    throw new NotSupportedException(
                        $"Dictionary key {key.FullName} on {property?.DeclaringType?.FullName}.{property?.Name} is not a string. " +
                        "Only string keys are exported.");
                }

                var valueInfo = info?.GenericTypeArguments.Length == 2 ? info.GenericTypeArguments[1] : null;
                var valueWire = BuildWire(value, valueInfo, null, property);
                if (valueInfo is not null && IsClrNullable(value, valueInfo))
                {
                    valueWire = valueWire.AsNullable();
                }

                IncludeElements(value, property);
                return new WireType(WireKind.Dictionary, false, DictionaryValue: valueWire);
            }

            if (underlying != typeof(byte[]) && IsCollection(underlying, out var element))
            {
                var elementInfo = underlying.IsArray ? info?.ElementType : info?.GenericTypeArguments.FirstOrDefault();
                var item = BuildWire(element, elementInfo, null, property);
                if (elementInfo is not null && IsClrNullable(element, elementInfo))
                {
                    item = item.AsNullable();
                }

                IncludeElements(element, property);
                return new WireType(WireKind.Array, false, Item: item);
            }

            if (IsScalar(underlying))
            {
                return new WireType(ClassifyScalar(underlying, converter), false);
            }

            Include(underlying, property);
            return new WireType(WireKind.Named, false, Name: TypeScriptName(underlying));
        }

        private void IncludeElements(Type element, PropertyInfo? via)
        {
            var underlying = Nullable.GetUnderlyingType(element) ?? element;
            if (!IsScalar(underlying) && !IsDictionary(underlying) && !IsCollection(underlying, out _))
            {
                Include(underlying, via);
            }
        }

        private WireKind ClassifyScalar(Type type, JsonConverterAttribute? converter)
        {
            if (type == typeof(string) || type == typeof(char))
            {
                return WireKind.String;
            }

            if (type == typeof(bool))
            {
                return WireKind.Boolean;
            }

            if (type == typeof(Guid))
            {
                return WireKind.Uuid;
            }

            if (type == typeof(Uri))
            {
                return WireKind.Uri;
            }

            if (type == typeof(TimeSpan))
            {
                return WireKind.TimeSpan;
            }

            if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                return ClassifyTemporal(type, converter);
            }

            if (type == typeof(byte) || type == typeof(sbyte)
                || type == typeof(short) || type == typeof(ushort)
                || type == typeof(int) || type == typeof(uint)
                || type == typeof(long) || type == typeof(ulong))
            {
                return WireKind.Integer;
            }

            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                return WireKind.Number;
            }

            throw new NotSupportedException($"No JSON contract mapping for {type.FullName}.");
        }

        private WireKind ClassifyTemporal(Type type, JsonConverterAttribute? converter)
        {
            object sample = type == typeof(DateTime)
                ? DateTime.SpecifyKind(new DateTime(2020, 6, 15, 13, 45, 30), DateTimeKind.Unspecified)
                : new DateTimeOffset(2020, 6, 15, 13, 45, 30, System.TimeSpan.FromHours(1));
            var json = JsonSerializer.Serialize(sample, ProbeOptions(converter));
            var text = JsonDocument.Parse(json).RootElement.GetString()
                       ?? throw new InvalidOperationException($"Date serialized as non-string: {json}");
            if (text.EndsWith('Z'))
            {
                return WireKind.IsoDateTime;
            }

            if (System.Text.RegularExpressions.Regex.IsMatch(text, WireFormats.OffsetDateTime))
            {
                return WireKind.OffsetDateTime;
            }

            throw new NotSupportedException(
                $"{type.Name} serialized as '{text}', which is neither a Z instant nor a numeric offset.");
        }

        private void AddEnumMembers(Type enumType, ContractType contract)
        {
            foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != enumType)
                {
                    continue;
                }

                var value = field.GetValue(null)!;
                contract.Members.Add(new EnumMember(field.Name, Convert.ToInt64(value), field.Name));
            }
        }

        private void RewriteMemberWire(Type enumType, ContractType contract, JsonConverterAttribute? converter)
        {
            var probe = ProbeOptions(converter);
            for (var i = 0; i < contract.Members.Count; i++)
            {
                var member = contract.Members[i];
                var value = Enum.Parse(enumType, member.Name);
                var json = JsonSerializer.Serialize(value, probe);
                var element = JsonDocument.Parse(json).RootElement;
                var wire = element.ValueKind == JsonValueKind.String
                    ? element.GetString()!
                    : element.GetRawText();
                contract.Members[i] = member with { WireText = wire };
            }
        }

        private EnumWire ProbeEnumStyle(Type enumType, JsonConverterAttribute? converter)
        {
            var value = Enum.GetValues(enumType).GetValue(0)!;
            var json = JsonSerializer.Serialize(value, ProbeOptions(converter));
            var kind = JsonDocument.Parse(json).RootElement.ValueKind;
            return kind == JsonValueKind.String ? EnumWire.String : EnumWire.Number;
        }

        private JsonSerializerOptions ProbeOptions(JsonConverterAttribute? converter)
        {
            var probe = new JsonSerializerOptions(options);
            if (converter?.ConverterType is not null)
            {
                if (Activator.CreateInstance(converter.ConverterType) is not JsonConverter instance)
                {
                    throw new NotSupportedException(
                        $"{converter.ConverterType.FullName} has no public parameterless constructor.");
                }

                probe.Converters.Insert(0, instance);
            }

            return probe;
        }

        private static bool CanFollow(Type type)
        {
            var ns = type.Namespace ?? "";
            return ContractExportRules.FollowNamespacePrefixes.Any(prefix =>
                ns.Equals(prefix, StringComparison.Ordinal)
                || ns.StartsWith(prefix + ".", StringComparison.Ordinal));
        }

        private static NotSupportedException OutsideApiDtos(Type type, PropertyInfo? via)
        {
            var site = via is null
                ? "An exported contract"
                : $"{via.DeclaringType?.FullName}.{via.Name}";
            return new NotSupportedException(
                $"{site} references {type.FullName}, which is outside Api.Dtos. " +
                "Add a boundary DTO in Api.Dtos with the same wire shape and map it where the DTO is built.");
        }

        private static bool IsClrNullable(Type type, NullabilityInfo info) =>
            Nullable.GetUnderlyingType(type) is not null
            || (!type.IsValueType && info.ReadState == NullabilityState.Nullable);

        private static bool IsScalar(Type type) =>
            type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(Uri)
            || type == typeof(byte[]);

        private static bool IsDictionary(Type type) => IsDictionary(type, out _, out _);

        private static bool IsDictionary(Type type, out Type key, out Type value)
        {
            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(Dictionary<,>) || def == typeof(IDictionary<,>) || def == typeof(IReadOnlyDictionary<,>))
                {
                    var args = type.GetGenericArguments();
                    key = args[0];
                    value = args[1];
                    return true;
                }
            }

            key = null!;
            value = null!;
            return false;
        }

        private static bool IsCollection(Type type, out Type element)
        {
            if (type.IsArray)
            {
                element = type.GetElementType()!;
                return true;
            }

            if (type.IsGenericType)
            {
                var def = type.GetGenericTypeDefinition();
                if (def == typeof(List<>) || def == typeof(IList<>) || def == typeof(ICollection<>)
                    || def == typeof(IEnumerable<>) || def == typeof(IReadOnlyList<>)
                    || def == typeof(IReadOnlyCollection<>))
                {
                    element = type.GetGenericArguments()[0];
                    return true;
                }
            }

            element = null!;
            return false;
        }

        private static string TypeScriptName(Type type)
        {
            if (type.DeclaringType is null)
            {
                return type.Name;
            }

            return TypeScriptName(type.DeclaringType) + type.Name;
        }

        private enum EnumWire
        {
            String,
            Number
        }
    }
}
