using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Indexer.Activities;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Providers;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// #1102: a podcast stored with <c>"removed": null</c> must be selected by the hourly query, because the C# parent-defer
/// check in <c>CatalogueParentsUpdater</c> (and <c>PodcastsUpdater</c>, <c>Publisher.IsRemoved</c>) treat null as not
/// removed. In Cosmos <c>null = false</c> is undefined, so <c>NOT IS_DEFINED(removed) OR removed = false</c> alone drops
/// an explicit null while the defer check still counts it as a live podcast, and neither the podcast nor its same-id
/// parent is indexed.
/// </summary>
public class IndexIdProviderRemovedNullRules
{
    private readonly AutoMocker _mocker = new();
    private Expression<Func<Podcast, bool>>? _selector;

    public IndexIdProviderRemovedNullRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions()));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Callback<Expression<Func<Podcast, bool>>>(selector => _selector = selector)
            .Returns(AsyncEnumerable.Empty<Podcast>());
        _mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.GetIndexableParentIds())
            .ReturnsAsync([]);
    }

    private static string ToCosmosSql(Expression<Func<Podcast, bool>> selector)
    {
        using var client = new CosmosClient(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });
        return client.GetContainer("db", "podcasts").GetItemLinqQueryable<Podcast>()
            .Where(selector)
            .ToQueryDefinition()
            .QueryText;
    }

    [Fact(DisplayName =
        "Hourly podcast query: a stored \"removed\": null is selected (removed undefined, null or false), because the " +
        "parent-defer check treats null as not removed and Cosmos drops null under 'removed = false' alone (#1102).")]
    public async Task hourly_query_selects_a_stored_removed_null()
    {
        // Arrange
        var activity = _mocker.CreateInstance<IndexIdProvider>();

        // Act
        await activity.RunAsync(_mocker.GetMock<TaskActivityContext>().Object, new IndexIdProviderRequest(1));

        // Assert
        _selector.Should().NotBeNull();
        var sql = ToCosmosSql(_selector!);
        sql.Should().Contain("NOT IS_DEFINED(root[\"removed\"])");
        sql.Should().Contain("(root[\"removed\"] = null)");
        sql.Should().Contain("(root[\"removed\"] = false)");
    }
}
