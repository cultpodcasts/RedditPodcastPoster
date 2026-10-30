using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Providers;
using Xunit;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// #1102: the indexable-podcast id query must agree with the C# "not removed" rule (<c>Removed != true</c>), so a
/// podcast stored with <c>"removed": null</c> is selected like one with no <c>removed</c> property.
/// </summary>
public class IndexablePodcastIdProviderRemovedNullRules
{
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Indexable podcast ids: the query selects a stored \"removed\": null as well as absent or false, because every " +
        "C# reader treats null as not removed and Cosmos drops null under 'removed = false' alone (#1102).")]
    public async Task indexable_podcast_query_selects_a_stored_removed_null()
    {
        // Arrange
        Expression<Func<Podcast, bool>>? selector = null;
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>(),
                It.IsAny<Expression<Func<Podcast, Guid>>>()))
            .Callback<Expression<Func<Podcast, bool>>, Expression<Func<Podcast, Guid>>>((s, _) => selector = s)
            .Returns(AsyncEnumerable.Empty<Guid>());

        // Act
        await foreach (var _ in _mocker.CreateInstance<IndexablePodcastIdProvider>().GetIndexablePodcastIds())
        {
        }

        // Assert
        selector.Should().NotBeNull();
        using var client = new CosmosClient(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                }
            });
        var sql = client.GetContainer("db", "podcasts").GetItemLinqQueryable<Podcast>()
            .Where(selector!).ToQueryDefinition().QueryText;
        sql.Should().Contain("NOT IS_DEFINED(root[\"removed\"])");
        sql.Should().Contain("(root[\"removed\"] = null)");
        sql.Should().Contain("(root[\"removed\"] = false)");
    }
}
