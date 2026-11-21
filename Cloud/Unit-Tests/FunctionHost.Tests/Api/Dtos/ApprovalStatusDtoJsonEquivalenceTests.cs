using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class ApprovalStatusDtoJsonEquivalenceTests
{
    private static readonly JsonSerializerOptions HttpOptions = ApiJson.CreateHttpSerializerOptions();

    [Theory(DisplayName =
        "When an approval status is mapped to ApprovalStatusDto and written with the episode property's string converter, the JSON matches the domain enum, so the wire value is unchanged.")]
    [InlineData(ApprovalStatus.AwaitingApproval)]
    [InlineData(ApprovalStatus.Rejected)]
    public void approval_status_string_json_matches_the_domain_enum(ApprovalStatus status)
    {
        // Arrange
        var domain = new DomainApprovalProperty { ApprovalStatus = status };
        var dto = new DtoApprovalProperty { ApprovalStatus = status.ToDto() };

        // Act
        var domainJson = JsonSerializer.Serialize(domain, HttpOptions);
        var dtoJson = JsonSerializer.Serialize(dto, HttpOptions);

        // Assert
        dtoJson.Should().Be(domainJson);
        ((int)status.ToDto()).Should().Be((int)status);
        status.ToDto().ToString().Should().Be(status.ToString());
    }

    [Fact(DisplayName =
        "When approval status is null, both the domain property and the DTO property omit the key, because WhenWritingNull is unchanged.")]
    public void null_approval_status_omits_the_same_key()
    {
        // Arrange
        var domain = new DomainApprovalProperty();
        var dto = new DtoApprovalProperty { ApprovalStatus = ((ApprovalStatus?)null).ToDto() };

        // Act
        var domainJson = JsonSerializer.Serialize(domain, HttpOptions);
        var dtoJson = JsonSerializer.Serialize(dto, HttpOptions);

        // Assert
        dto.ApprovalStatus.Should().BeNull();
        dtoJson.Should().Be(domainJson);
        dtoJson.Should().Be("{}");
    }

    [Theory(DisplayName =
        "When an approval status is written as a number, the DTO enum matches the domain enum, so a converter-less payload is unchanged.")]
    [InlineData(ApprovalStatus.AwaitingApproval)]
    [InlineData(ApprovalStatus.Rejected)]
    public void approval_status_numeric_json_matches_the_domain_enum(ApprovalStatus status)
    {
        // Arrange
        var options = new JsonSerializerOptions(HttpOptions);

        // Act
        var domainJson = JsonSerializer.Serialize(status, options);
        var dtoJson = JsonSerializer.Serialize(status.ToDto(), options);

        // Assert
        dtoJson.Should().Be(domainJson);
    }

    [Fact(DisplayName =
        "When every ApprovalStatus value is mapped, the DTO has the same names and numbers, so the API enum cannot drift from the domain enum.")]
    public void approval_status_dto_matches_every_domain_member()
    {
        // Arrange
        var domainValues = Enum.GetValues<ApprovalStatus>();

        // Act
        var mapped = domainValues.Select(value => (Source: value, Dto: value.ToDto())).ToArray();

        // Assert
        Enum.GetValues<ApprovalStatusDto>().Should().HaveSameCount(domainValues);
        mapped.Should().AllSatisfy(pair =>
        {
            pair.Dto.ToString().Should().Be(pair.Source.ToString());
            ((int)pair.Dto).Should().Be((int)pair.Source);
        });
    }

    [Fact(DisplayName =
        "When an undefined approval status is mapped, it throws, so a new domain member cannot be sent under the old JSON name.")]
    public void undefined_approval_status_throws()
    {
        // Arrange
        var undefined = (ApprovalStatus)int.MaxValue;

        // Act
        var act = () => undefined.ToDto();

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>The episode JSON property as it was when the domain enum was serialized directly.</summary>
    private sealed class DomainApprovalProperty
    {
        [JsonPropertyName("approvalStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ApprovalStatus? ApprovalStatus { get; set; }
    }

    /// <summary>The same property after the boundary enum.</summary>
    private sealed class DtoApprovalProperty
    {
        [JsonPropertyName("approvalStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ApprovalStatusDto? ApprovalStatus { get; set; }
    }
}
