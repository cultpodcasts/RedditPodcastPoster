using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using Api.Dtos.Mapping;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class ServiceDtoJsonEquivalenceTests
{
    // Functions worker default serializer used by HandlerContext.Ok -> WriteAsJsonAsync.
    private static readonly JsonSerializerOptions ApiOptions = new(JsonSerializerDefaults.Web);
    private readonly Fixture _fixture = new();

    private Uri CreateUri() => new($"https://example.com/{_fixture.Create<Guid>()}");

    private static void ShouldSerializeIdentically<TModel, TDto>(TModel model, TDto dto) =>
        JsonSerializer.Serialize(dto, ApiOptions).Should().Be(JsonSerializer.Serialize(model, ApiOptions));

    [Theory(DisplayName =
        "When episode ids are mapped to EpisodeIdsDto, the JSON is byte-identical to the domain model (populated or empty), so API consumers see no change.")]
    [InlineData(true)]
    [InlineData(false)]
    public void episode_ids_json_is_identical(bool populated)
    {
        // Arrange
        var model = populated
            ? new EpisodeIds { Spotify = _fixture.Create<string>(), Apple = _fixture.Create<long>(), YouTube = _fixture.Create<string>() }
            : new EpisodeIds();

        // Act
        var dto = model.ToDto();

        // Assert
        ShouldSerializeIdentically(model, dto);
    }

    [Fact(DisplayName =
        "When null episode ids are mapped, the DTO is null, so the ids property is still serialised as null.")]
    public void null_episode_ids_map_to_null()
    {
        // Arrange
        EpisodeIds? model = null;

        // Act
        var dto = model.ToDto();

        // Assert
        dto.Should().BeNull();
    }

    [Theory(DisplayName =
        "When service urls are mapped to ServiceUrlsDto, the JSON is byte-identical to the domain model including property order and nulls.")]
    [InlineData(true)]
    [InlineData(false)]
    public void service_urls_json_is_identical(bool populated)
    {
        // Arrange
        var model = populated
            ? new ServiceUrls { Spotify = CreateUri(), Apple = CreateUri(), YouTube = CreateUri(), InternetArchive = CreateUri(), BBC = CreateUri() }
            : new ServiceUrls();

        // Act
        var dto = model.ToDto();

        // Assert
        ShouldSerializeIdentically(model, dto);
    }

    [Theory(DisplayName =
        "When episode images are mapped to EpisodeImagesDto, the JSON is byte-identical to the domain model including property order and nulls.")]
    [InlineData(true)]
    [InlineData(false)]
    public void episode_images_json_is_identical(bool populated)
    {
        // Arrange
        var model = populated
            ? new EpisodeImages { YouTube = CreateUri(), Spotify = CreateUri(), Apple = CreateUri(), Other = CreateUri() }
            : new EpisodeImages();

        // Act
        var dto = model.ToDto();

        // Assert
        ShouldSerializeIdentically(model, dto);
    }

    [Theory(DisplayName =
        "When service links are mapped to ServiceLinkDto, the JSON is byte-identical to the domain model, including omitting null url, image and lang.")]
    [InlineData(true)]
    [InlineData(false)]
    public void service_links_json_is_identical(bool populated)
    {
        // Arrange
        var model = new Dictionary<string, ServiceLink>
        {
            [_fixture.Create<string>()] = populated
                ? new ServiceLink { Url = CreateUri(), Image = CreateUri(), Language = _fixture.Create<string>() }
                : new ServiceLink()
        };

        // Act
        var dto = model.ToDtos();

        // Assert
        ShouldSerializeIdentically(model, dto);
    }

    [Theory(DisplayName =
        "When a service is mapped to ServiceDto, both its string-enum and numeric JSON are identical to the domain enum, so releaseAuthority and primaryPostService are unchanged.")]
    [InlineData(Service.Spotify)]
    [InlineData(Service.Apple)]
    [InlineData(Service.YouTube)]
    [InlineData(Service.Other)]
    public void service_enum_json_is_identical(Service service)
    {
        // Arrange
        Service? model = service;
        var stringOptions = new JsonSerializerOptions(ApiOptions) { Converters = { new JsonStringEnumConverter() } };

        // Act
        var dto = model.ToDto();

        // Assert
        JsonSerializer.Serialize(dto, stringOptions).Should().Be(JsonSerializer.Serialize(model, stringOptions));
        JsonSerializer.Serialize(dto, ApiOptions).Should().Be(JsonSerializer.Serialize(model, ApiOptions));
    }

    [Fact(DisplayName =
        "When a null service is mapped, the DTO is null, so releaseAuthority and primaryPostService still serialise as null.")]
    public void null_service_maps_to_null()
    {
        // Arrange
        Service? model = null;

        // Act
        var dto = model.ToDto();

        // Assert
        dto.Should().BeNull();
    }

    [Theory(DisplayName =
        "When discovery result urls are mapped to DiscoveryResultUrlsDto, the JSON is byte-identical to the domain model including nulls.")]
    [InlineData(true)]
    [InlineData(false)]
    public void discovery_result_urls_json_is_identical(bool populated)
    {
        // Arrange
        var model = populated
            ? new DiscoveryResultUrls { Spotify = CreateUri(), Apple = CreateUri(), YouTube = CreateUri() }
            : new DiscoveryResultUrls();

        // Act
        var dto = model.ToDto();

        // Assert
        ShouldSerializeIdentically(model, dto);
    }

    [Fact(DisplayName =
        "When every Service value is mapped, it becomes the ServiceDto with the same name and number, and both enums have the same members, so the API contract can't drift from the domain.")]
    public void service_dto_has_parity_with_service()
    {
        // Arrange
        var services = Enum.GetValues<Service>();

        // Act
        var mapped = services.Select(s => (Source: s, Dto: s.ToDto())).ToArray();

        // Assert
        Enum.GetValues<ServiceDto>().Should().HaveSameCount(services);
        mapped.Should().AllSatisfy(m =>
        {
            m.Dto.ToString().Should().Be(m.Source.ToString());
            ((int)m.Dto).Should().Be((int)m.Source);
        });
    }

    [Fact(DisplayName =
        "When an undefined Service value is mapped, it throws, so an unmapped enum member fails loudly instead of being silently renumbered.")]
    public void undefined_service_throws()
    {
        // Arrange
        var undefined = (Service)int.MaxValue;

        // Act
        var act = () => undefined.ToDto();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
