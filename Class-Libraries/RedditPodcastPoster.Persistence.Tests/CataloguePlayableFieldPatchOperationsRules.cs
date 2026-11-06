using FluentAssertions;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class CataloguePlayableFieldPatchOperationsRules
{
    [Fact(DisplayName =
        "Playable field patch: clearing services or youTubeId removes those paths, because an explicit JSON null " +
        "is a different document shape from a full save that omits them (#1154).")]
    public void cleared_fields_are_removed()
    {
        // Arrange
        var patch = new CataloguePlayableFieldPatch
        {
            SetServices = true,
            Services = null,
            SetYouTubeId = true,
            YouTubeId = null
        };

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Remove);
        operations.Select(x => x.Path).Should().BeEquivalentTo("/services", "/youTubeId");
    }

    [Fact(DisplayName =
        "Playable field patch: a services map and a youTube id are set on those paths only, because the patch " +
        "must not touch any other playable field.")]
    public void present_fields_are_set()
    {
        // Arrange
        var services = new Dictionary<string, ServiceLink>
        {
            ["youtube"] = new ServiceLink { Url = new Uri("https://www.youtube.com/watch?v=abcdefghijk") }
        };
        var patch = new CataloguePlayableFieldPatch
        {
            SetServices = true,
            Services = services,
            SetYouTubeId = true,
            YouTubeId = "abcdefghijk"
        };

        // Act
        var operations = CataloguePlayableFieldPatchOperations.Build(patch);

        // Assert
        operations.Should().HaveCount(2);
        operations.Should().OnlyContain(x => x.OperationType == PatchOperationType.Set);
        operations.Select(x => x.Path).Should().BeEquivalentTo("/services", "/youTubeId");
    }
}
