using AutoFixture;
using FluentAssertions;
using Api.Dtos.Extensions;
using Api.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class DiscoveryResponseAwaitingReviewTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "Curation GET lists reports held for supervisor review under awaitingReview, because curators must see they exist but cannot curate them.")]
    public void awaiting_review_maps_to_dto()
    {
        // Arrange
        var held = new DiscoveryAwaitingReview(_fixture.Create<Guid>(), _fixture.Create<DateTime>(), _fixture.Create<int>());
        var data = new DiscoveryCurationData([], [], new Dictionary<Guid, DiscoveryPodcastInfo>(), 0, [held]);

        // Act
        var dto = data.ToDto();

        // Assert
        var item = dto.AwaitingReview.Should().ContainSingle().Subject;
        item.Id.Should().Be(held.DocumentId);
        item.DiscoveryBegan.Should().Be(held.DiscoveryBegan);
        item.ResultCount.Should().Be(held.ResultCount);
    }
}
