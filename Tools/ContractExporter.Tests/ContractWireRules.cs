using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.Dtos;
using Api.Serialization;
using AutoFixture;
using ContractExporter;
using FluentAssertions;
using Xunit;

namespace ContractExporter.Tests;

public class ContractWireRules
{
    private readonly Fixture _fixture = new();
    private readonly JsonSerializerOptions _options = ApiJson.CreateHttpSerializerOptions();

    [Fact(DisplayName =
        "When the contract graph is built, then the pilot roots and the DTOs and enums they reference are exported and unrelated DTOs are not, because generation follows attributes and the reference closure rather than every file in Api.Dtos.")]
    public void pilot_closure_exports_referenced_types_only()
    {
        // Arrange
        var expected =
            new[]
            {
                "ApprovalStatusDto",
                "ContentKindDto",
                "EpisodeDto",
                "EpisodeDtoMatchResult",
                "EpisodeDtoPersonMatch",
                "EpisodeIdsDto",
                "EpisodeImagesDto",
                "EpisodeTrailerAttachRequest",
                "EpisodeTrailerAttachResponse",
                "EpisodeTvShowTrailerAttachRequest",
                "EpisodeTvShowTrailerAttachResponse",
                "EpisodeUpdateResponse",
                "FilmTrailerChangeRequest",
                "FilmTrailerDto",
                "PersonDto",
                "PublicEpisodeDto",
                "SearchIndexerState",
                "ServiceDto",
                "ServiceLinkDto",
                "ServiceUrlsDto",
                "SubjectMatchDto",
                "SubjectMatchSourceDto",
                "SubmitUrlResponseIdentityCandidateBody",
                "SubmitUrlResponseIdentityDecisionBody",
                "TvShowTrailerChangeRequest",
                "TvShowTrailerDto",
                "YouTubeAlternateDto"
            };

        // Act
        var names = ContractGraph.Build(_options).Types.Select(type => type.Name).ToArray();

        // Assert
        names.Should().BeEquivalentTo(expected);
    }

    [Fact(DisplayName =
        "When an EpisodeUpdateResponse is written by the HTTP serializer, then null fields, the string SearchIndexerState and property order match the generated contract, because clients see that JSON.")]
    public void episode_update_response_matches_http_json()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var contract = document.Types.Single(type => type.Name == "EpisodeUpdateResponse");
        var executed = new EpisodeUpdateResponse
        {
            TweetDeleted = null,
            BlueskyPostDeleted = true,
            SearchIndexerState = SearchIndexerState.Executed
        };

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(executed, _options));

        // Assert
        json.RootElement.GetProperty("searchIndexerState").GetString().Should().Be(nameof(SearchIndexerState.Executed));
        json.RootElement.GetProperty("blueskyPostDeleted").GetBoolean().Should().BeTrue();
        AssertNullHandling(json.RootElement, contract, executed);
        AssertPropertyOrder(contract, executed);
    }

    [Fact(DisplayName =
        "When an EpisodeDto is written by the HTTP serializer, then dates end in Z, durations and guids are strings, string enums use member names, WhenWritingNull fields are omitted, and property order matches the generated contract.")]
    public void episode_dto_matches_http_json()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var contract = document.Types.Single(type => type.Name == "EpisodeDto");
        var episode = new EpisodeDto();
        Populate(episode, 0);
        episode.Length = TimeSpan.FromMinutes(90);
        episode.Release = DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc).AddHours(2);
        episode.ReleaseAuthority = ServiceDto.YouTube;
        episode.ApprovalStatus = null;
        episode.ContentKind = null;
        episode.AlternativeTitles = null;
        episode.ImageRemoved = null;

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(episode, _options));

        // Assert
        json.RootElement.GetProperty("duration").GetString().Should().Be("01:30:00");
        json.RootElement.GetProperty("release").GetString().Should().MatchRegex(WireFormats.IsoDateTime);
        json.RootElement.GetProperty("releaseAuthority").GetString().Should().Be(nameof(ServiceDto.YouTube));
        json.RootElement.GetProperty("id").GetString().Should().MatchRegex(
            "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
        json.RootElement.TryGetProperty("contentKind", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("alternativeTitles", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("approvalStatus", out _).Should().BeFalse();
        json.RootElement.TryGetProperty("imageRemoved", out _).Should().BeFalse();
        json.RootElement.GetProperty("subjects").ValueKind.Should().Be(JsonValueKind.Array); // pragma: allowlist secret
        json.RootElement.GetProperty("removedSubjects").ValueKind.Should().Be(JsonValueKind.Array); // pragma: allowlist secret
        json.RootElement.GetProperty("matches").ValueKind.Should().Be(JsonValueKind.Array);
        AssertNullHandling(JsonDocument.Parse(JsonSerializer.Serialize(new EpisodeDto(), _options)).RootElement, contract, new EpisodeDto());
        AssertPropertyOrder(contract, episode);
    }

    [Fact(DisplayName =
        "When a PublicEpisodeDto is written by the HTTP serializer, then imageRemoved and the kind fields are omitted when null, image is written as null, and property order matches the generated contract, because website bookmarks read /public/episode/{id} through that contract (#1502).")]
    public void public_episode_dto_matches_http_json()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var contract = document.Types.Single(type => type.Name == "PublicEpisodeDto");
        var removed = new PublicEpisodeDto();
        Populate(removed, 0);
        removed.Image = null;
        removed.ImageRemoved = true;
        var kept = new PublicEpisodeDto();

        // Act
        using var removedJson = JsonDocument.Parse(JsonSerializer.Serialize(removed, _options));
        using var keptJson = JsonDocument.Parse(JsonSerializer.Serialize(kept, _options));

        // Assert
        removedJson.RootElement.GetProperty("imageRemoved").GetBoolean().Should().BeTrue();
        removedJson.RootElement.GetProperty("image").ValueKind.Should().Be(JsonValueKind.Null);
        keptJson.RootElement.TryGetProperty("imageRemoved", out _).Should().BeFalse();
        keptJson.RootElement.TryGetProperty("contentKind", out _).Should().BeFalse();
        keptJson.RootElement.GetProperty("image").ValueKind.Should().Be(JsonValueKind.Null);
        contract.Properties.Single(property => property.JsonName == "imageRemoved").Optional.Should().BeTrue();
        AssertNullHandling(keptJson.RootElement, contract, kept);
        AssertPropertyOrder(contract, removed);
    }

    [Fact(DisplayName =
        "When TypeScript and Zod are rendered, then PublicEpisodeDto has an optional nullable imageRemoved and a required nullable image, matching the WhenWritingNull attributes on the C# DTO (#1502).")]
    public void rendered_public_episode_has_optional_image_removed()
    {
        // Arrange
        var document = ContractGraph.Build(_options);

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);

        // Assert
        files[ContractPaths.TypeScript].Should().Contain("export interface PublicEpisodeDto {");
        files[ContractPaths.TypeScript].Should().Contain("\n  imageRemoved?: boolean | null;\n");
        files[ContractPaths.Zod].Should().Contain("publicEpisodeDtoSchema");
        files[ContractPaths.Zod].Should().Contain("imageRemoved: z.boolean().nullable().optional(),");
        files[ContractPaths.JsonSchema].Should().Contain("\"PublicEpisodeDto\"");
    }

    [Fact(DisplayName =
        "When TypeScript and Zod are rendered for the pilot, then the episode contract includes subjects, removed subjects, alternative titles, matches and searchIndexerState, because those are on the C# DTOs.")] // pragma: allowlist secret
    public void rendered_contracts_include_fields_missing_from_hand_written_clients()
    {
        // Arrange
        var document = ContractGraph.Build(_options);

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);

        // Assert
        files[ContractPaths.TypeScript].Should().Contain("removedSubjects"); // pragma: allowlist secret
        files[ContractPaths.TypeScript].Should().Contain("alternativeTitles");
        files[ContractPaths.TypeScript].Should().Contain("matches");
        files[ContractPaths.TypeScript].Should().Contain("searchIndexerState");
        files[ContractPaths.Zod].Should().Contain("removedSubjects"); // pragma: allowlist secret
        files[ContractPaths.Zod].Should().Contain("searchIndexerStateSchema");
        files[ContractPaths.JsonSchema].Should().Contain("EpisodeNotFound");
    }

    [Fact(DisplayName =
        "When the exporter has run as part of the build, then the gitignored contract folder matches a fresh render, because CI typechecks that generated output rather than a committed copy.")]
    public void build_output_matches_the_renderer()
    {
        // Arrange
        var directory = RepositoryContracts();
        var document = ContractGraph.Build(_options);

        // Act
        var differences = ContractText.Differences(directory, document);

        // Assert
        differences.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When HTTP serializer options are created, then null is written and property names are case-insensitive, because those are the isolated-worker defaults on WorkerOptions.Serializer.")]
    public void http_options_follow_the_worker_serializer()
    {
        // Arrange
        var options = ApiJson.CreateHttpSerializerOptions();

        // Act
        var ignoreNull = options.DefaultIgnoreCondition;
        var caseInsensitive = options.PropertyNameCaseInsensitive;
        var json = JsonSerializer.Serialize(new EpisodeDto { Language = null, AppleId = null }, options);

        // Assert
        ignoreNull.Should().Be(JsonIgnoreCondition.Never);
        caseInsensitive.Should().BeTrue();
        json.Should().Contain("\"lang\":null");
        json.Should().Contain("\"appleId\":null");
    }

    [Fact(DisplayName =
        "When a nullable property has no WhenWritingNull or WhenWritingDefault ignore, then the generated contract requires the key and allows null, because the worker writes JSON null rather than omitting the key.")]
    public void nullable_without_ignore_is_required_and_nullable()
    {
        // Arrange
        var document = ContractGraph.Build(_options);

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);

        // Assert
        files[ContractPaths.TypeScript].Should().Contain("\n  appleId: number | null;\n");
        files[ContractPaths.TypeScript].Should().Contain("\n  lang: string | null;\n");
        files[ContractPaths.TypeScript].Should().Contain("\n  approvalStatus?: ApprovalStatusDto | null;\n");
        files[ContractPaths.Zod].Should().Contain("appleId: z.number().int().nullable(),");
        files[ContractPaths.Zod].Should().Contain("lang: z.string().nullable(),");
        files[ContractPaths.Zod].Should().Contain("approvalStatus: approvalStatusDtoSchema.nullable().optional(),");
    }

    [Fact(DisplayName =
        "When the contract is rendered, then contentKind on EpisodeDto and PublicEpisodeDto is the ContentKindDto string enum, exported as a const object plus a type of the same name, so clients compare against named members instead of string literals (#1508).")]
    public void content_kind_exports_as_a_string_enum_const_object()
    {
        // Arrange
        var document = ContractGraph.Build(_options);

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);
        var shape = document.Types.Single(type => type.Name == "ContentKindDto").Shape;

        // Assert
        shape.Should().Be(ContractShape.StringEnum);
        files[ContractPaths.TypeScript].Should().Contain(
            "export const ContentKindDto = {\n  Episode: \"Episode\",\n  TvShowEpisode: \"TvShowEpisode\",\n  Film: \"Film\",\n  NewsReport: \"NewsReport\",\n} as const;\n");
        files[ContractPaths.TypeScript].Should().Contain(
            "export type ContentKindDto = (typeof ContentKindDto)[keyof typeof ContentKindDto];");
        files[ContractPaths.TypeScript].Should().NotContain("contentKind?: string");
        files[ContractPaths.TypeScript].Should().Contain("\n  contentKind?: ContentKindDto | null;\n");
        files[ContractPaths.Zod].Should().Contain("contentKind: contentKindDtoSchema.nullable().optional(),");
    }

    [Fact(DisplayName =
        "When the contract is rendered, then source on SubjectMatchDto is the SubjectMatchSourceDto string enum, exported as a const object plus a type of the same name, so clients compare against named members instead of string literals (#1513).")]
    public void subject_match_source_exports_as_a_string_enum_const_object()
    {
        // Arrange
        var document = ContractGraph.Build(_options);

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);
        var shape = document.Types.Single(type => type.Name == "SubjectMatchSourceDto").Shape;
        var subjectMatch = document.Types.Single(type => type.Name == "SubjectMatchDto");

        // Assert
        shape.Should().Be(ContractShape.StringEnum);
        subjectMatch.Properties.Single(property => property.JsonName == "source").Type.Name.Should()
            .Be("SubjectMatchSourceDto");
        files[ContractPaths.TypeScript].Should().Contain(
            "export const SubjectMatchSourceDto = {\n  Title: \"Title\",\n  Description: \"Description\",\n  PodcastDefault: \"PodcastDefault\",\n} as const;\n");
        files[ContractPaths.TypeScript].Should().Contain(
            "export type SubjectMatchSourceDto = (typeof SubjectMatchSourceDto)[keyof typeof SubjectMatchSourceDto];");
        var subjectMatchInterface = Regex.Match(
            files[ContractPaths.TypeScript],
            @"export interface SubjectMatchDto \{.*?\n\}", RegexOptions.Singleline).Value;
        subjectMatchInterface.Should().NotBeEmpty();
        subjectMatchInterface.Should().NotContain("\n  source: string;\n");
        subjectMatchInterface.Should().Contain("\n  source: SubjectMatchSourceDto;\n");
        files[ContractPaths.Zod].Should().Contain("source: subjectMatchSourceDtoSchema,");
    }

    [Fact(DisplayName =
        "When TypeScript and Zod are rendered, then EpisodeDto has an optional imageRemoved flag, because the curator episode JSON omits it unless a curator removed the image (RPP #1500, #1502).")]
    public void episode_contract_has_optional_image_removed()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var contract = document.Types.Single(type => type.Name == "EpisodeDto");

        // Act
        var files = ContractText.Render(document).ToDictionary(file => file.RelativePath, file => file.Contents);

        // Assert
        contract.Properties.Single(property => property.JsonName == "imageRemoved").Optional.Should().BeTrue();
        files[ContractPaths.TypeScript].Should().Contain("export interface EpisodeDto {");
        files[ContractPaths.TypeScript].Should().Contain("\n  imageRemoved?: boolean | null;\n");
        files[ContractPaths.Zod].Should().Contain("episodeDtoSchema");
        files[ContractPaths.Zod].Should().Contain("imageRemoved: z.boolean().nullable().optional(),");
        files[ContractPaths.JsonSchema].Should().Contain("\"EpisodeDto\"");
    }

    [Fact(DisplayName =
        "When an exported property's type is outside Api.Dtos, then the build throws and names that property, because a domain type has to be mapped to a boundary DTO before it can change the package.")]
    public void property_outside_api_dtos_fails_and_names_the_property()
    {
        // Arrange
        var roots = new[] { typeof(OutsideBoundaryProbeDto) };

        // Act
        var act = () => ContractGraph.Build(roots, _options);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Api.Dtos.OutsideBoundaryProbeDto.OutsideKind*ContractExporter.Tests.Probes.OutsideKind*");
    }

    [Fact(DisplayName =
        "When an exported DTO references itself, then rendering throws before any contract file is written, because a Zod schema cannot mention itself while it is still being created.")]
    public void self_reference_is_a_cycle_and_writes_nothing()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "contract-cycle-" + _fixture.Create<Guid>().ToString("N"));
        Directory.CreateDirectory(directory);
        var document = ContractGraph.Build(new[] { typeof(SelfReferenceProbeDto) }, _options);

        // Act
        var act = () => ContractText.Write(directory, document);

        // Assert
        try
        {
            act.Should().Throw<NotSupportedException>().WithMessage("*SelfReferenceProbeDto*");
            Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact(DisplayName =
        "When a list of an Api.Dtos enum uses ItemConverterDecorator of JsonStringEnumConverter, then the contract uses member names, because that factory writes each element with the item converter.")]
    public void item_converter_on_a_list_emits_enum_names()
    {
        // Arrange
        var roots = new[] { typeof(ItemEnumProbeDto) };

        // Act
        var document = ContractGraph.Build(roots, _options);
        var zod = ContractText.Render(document).Single(file => file.RelativePath == ContractPaths.Zod).Contents;

        // Assert
        document.Types.Single(type => type.Name == nameof(ItemEnumProbe)).Shape.Should().Be(ContractShape.StringEnum);
        zod.Should().Contain("z.enum(");
        zod.Should().Contain("\"First\"");
        zod.Should().Contain("\"Second\"");
        zod.Should().NotContain("z.literal(0)");
    }

    [Fact(DisplayName =
        "When one use of an enum is a number and another is an item-converter list of names, then the build throws, because the contract cannot pick one wire form for both.")]
    public void disagreeing_enum_uses_fail_the_build()
    {
        // Arrange
        var roots = new[] { typeof(ItemEnumSplitProbeDto) };

        // Act
        var act = () => ContractGraph.Build(roots, _options);

        // Assert
        act.Should().Throw<NotSupportedException>().WithMessage("*SplitEnumProbe*");
    }

    [Fact(DisplayName =
        "When a film or TV show trailer is written by the HTTP serializer, then release ends in Z, duration is a string, image and release are omitted when null, and property order matches the generated contract, because the curator PATCH and read models share that wire shape (#1602).")]
    public void trailer_dtos_match_http_json()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var film = new FilmTrailerDto();
        var show = new TvShowTrailerDto();
        Populate(film, 0);
        Populate(show, 0);
        var bare = new FilmTrailerDto();

        // Act
        using var filmJson = JsonDocument.Parse(JsonSerializer.Serialize(film, _options));
        using var showJson = JsonDocument.Parse(JsonSerializer.Serialize(show, _options));
        using var bareJson = JsonDocument.Parse(JsonSerializer.Serialize(bare, _options));

        // Assert
        filmJson.RootElement.GetProperty("release").GetString().Should().MatchRegex(WireFormats.IsoDateTime);
        showJson.RootElement.GetProperty("release").GetString().Should().MatchRegex(WireFormats.IsoDateTime);
        filmJson.RootElement.GetProperty("duration").ValueKind.Should().Be(JsonValueKind.String);
        bareJson.RootElement.TryGetProperty("image", out _).Should().BeFalse();
        bareJson.RootElement.TryGetProperty("release", out _).Should().BeFalse();
        bareJson.RootElement.GetProperty("url").ValueKind.Should().Be(JsonValueKind.Null);
        AssertNullHandling(bareJson.RootElement, document.Types.Single(type => type.Name == "FilmTrailerDto"), bare);
        AssertPropertyOrder(document.Types.Single(type => type.Name == "FilmTrailerDto"), film);
        AssertPropertyOrder(document.Types.Single(type => type.Name == "TvShowTrailerDto"), show);
    }

    [Fact(DisplayName =
        "When a trailer attach response is written by the HTTP serializer, then error, filmCreated, cancelled and identityDecision are omitted by default, filmId is written as null, and the contract marks them optional, because the website reads the outcome through that contract (#1602).")]
    public void trailer_attach_response_matches_http_json()
    {
        // Arrange
        var document = ContractGraph.Build(_options);
        var contract = document.Types.Single(type => type.Name == "EpisodeTrailerAttachResponse");
        var empty = new EpisodeTrailerAttachResponse();
        var tvEmpty = new EpisodeTvShowTrailerAttachResponse();

        // Act
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(empty, _options));
        using var tvJson = JsonDocument.Parse(JsonSerializer.Serialize(tvEmpty, _options));

        // Assert
        foreach (var omitted in new[] { "error", "filmCreated", "cancelled", "identityDecision" })
        {
            json.RootElement.TryGetProperty(omitted, out _).Should().BeFalse();
            contract.Properties.Single(property => property.JsonName == omitted).Optional.Should().BeTrue();
        }

        json.RootElement.GetProperty("filmId").ValueKind.Should().Be(JsonValueKind.Null);
        json.RootElement.GetProperty("failureIndexing").GetBoolean().Should().BeFalse();
        tvJson.RootElement.TryGetProperty("showCreated", out _).Should().BeFalse();
        AssertNullHandling(json.RootElement, contract, empty);
    }

    private void AssertNullHandling(JsonElement json, ContractType contract, object instance)
    {
        foreach (var property in contract.Properties)
        {
            var value = ClrProperty(instance.GetType(), property.JsonName).GetValue(instance);
            if (value is not null)
            {
                continue;
            }

            var present = json.TryGetProperty(property.JsonName, out var element);
            if (property.Optional)
            {
                present.Should().BeFalse("{0} is optional, so a null is omitted", property.JsonName);
            }
            else
            {
                present.Should().BeTrue("{0} is required and nullable, so null is written", property.JsonName);
                property.Type.Nullable.Should().BeTrue();
                element.ValueKind.Should().Be(JsonValueKind.Null);
            }
        }
    }

    private void AssertPropertyOrder(ContractType contract, object instance)
    {
        var populated = JsonSerializer.SerializeToElement(instance, _options);
        var written = populated.EnumerateObject().Select(property => property.Name).ToArray();
        var expected = contract.Properties
            .Where(property => written.Contains(property.JsonName))
            .Select(property => property.JsonName)
            .ToArray();
        written.Should().Equal(expected);
    }

    private static PropertyInfo ClrProperty(Type type, string jsonName) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Single(property =>
                (property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name) == jsonName);

    private object? Sample(Type type, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(string))
        {
            return "wire";
        }

        if (underlying == typeof(bool))
        {
            return true;
        }

        if (underlying == typeof(Guid))
        {
            return _fixture.Create<Guid>();
        }

        if (underlying == typeof(DateTime))
        {
            return DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Utc).AddHours(2);
        }

        if (underlying == typeof(DateTimeOffset))
        {
            return DateTimeOffset.UnixEpoch;
        }

        if (underlying == typeof(TimeSpan))
        {
            return TimeSpan.FromMinutes(90);
        }

        if (underlying == typeof(Uri))
        {
            return new Uri("https://example.com/item");
        }

        if (underlying.IsEnum)
        {
            return Enum.GetValues(underlying).GetValue(0);
        }

        if (underlying == typeof(byte) || underlying == typeof(short) || underlying == typeof(int)
            || underlying == typeof(long) || underlying == typeof(sbyte) || underlying == typeof(ushort)
            || underlying == typeof(uint) || underlying == typeof(ulong))
        {
            return Convert.ChangeType(7, underlying);
        }

        if (underlying == typeof(float) || underlying == typeof(double) || underlying == typeof(decimal))
        {
            return Convert.ChangeType(1.5, underlying);
        }

        if (depth > 4)
        {
            throw new InvalidOperationException($"Sample graph is too deep at {underlying.FullName}.");
        }

        if (TryDictionary(underlying, out var key, out var value))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(ConcreteCollection(underlying))!;
            var keySample = Sample(key, depth + 1)
                            ?? throw new InvalidOperationException($"No sample for dictionary key {key.FullName}.");
            dictionary.Add(keySample, Sample(value, depth + 1));
            return dictionary;
        }

        if (TryCollection(underlying, out var element))
        {
            if (underlying.IsArray)
            {
                var array = Array.CreateInstance(element, 1);
                array.SetValue(Sample(element, depth + 1), 0);
                return array;
            }

            var list = (IList)Activator.CreateInstance(ConcreteCollection(underlying))!;
            list.Add(Sample(element, depth + 1));
            return list;
        }

        var created = Activator.CreateInstance(underlying)
                      ?? throw new InvalidOperationException($"Cannot construct {underlying.FullName}.");
        Populate(created, depth + 1);
        return created;
    }

    private void Populate(object instance, int depth)
    {
        foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod is not { IsPublic: true } || property.SetMethod is not { IsPublic: true })
            {
                continue;
            }

            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            property.SetValue(instance, Sample(property.PropertyType, depth));
        }
    }

    private static bool TryDictionary(Type type, out Type key, out Type value)
    {
        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>)
                || definition == typeof(IReadOnlyDictionary<,>))
            {
                var arguments = type.GetGenericArguments();
                key = arguments[0];
                value = arguments[1];
                return true;
            }
        }

        key = null!;
        value = null!;
        return false;
    }

    private static bool TryCollection(Type type, out Type element)
    {
        if (type.IsArray)
        {
            element = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(List<>) || definition == typeof(IList<>) || definition == typeof(ICollection<>)
                || definition == typeof(IEnumerable<>) || definition == typeof(IReadOnlyList<>)
                || definition == typeof(IReadOnlyCollection<>))
            {
                element = type.GetGenericArguments()[0];
                return true;
            }
        }

        element = null!;
        return false;
    }

    private static Type ConcreteCollection(Type type)
    {
        if (!type.IsInterface || !type.IsGenericType)
        {
            return type;
        }

        var definition = type.GetGenericTypeDefinition();
        if (definition == typeof(IDictionary<,>) || definition == typeof(IReadOnlyDictionary<,>))
        {
            return typeof(Dictionary<,>).MakeGenericType(type.GetGenericArguments());
        }

        return typeof(List<>).MakeGenericType(type.GetGenericArguments()[0]);
    }

    private static string RepositoryContracts()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "RedditPodcastPoster.slnx")))
            {
                return Path.Combine(dir.FullName, "contracts");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root was not found from the test output directory.");
    }
}
