using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Episodes;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1370: the shared "news organisation holds this YouTube channel" filter is the first non-test Cosmos query that
/// runs <c>.Any(...)</c> over a nested array. The business rules run it in memory, so only these facts prove the
/// Cosmos SDK turns it into SQL over the stored camelCase names; if it did not, every owner lookup, uniqueness check
/// and channel-wide claim check would match no news organisation in production while every other test still passed.
/// </summary>
public class NewsOrganisationCosmosFilterTranslationRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();

    private readonly CosmosClient _client = new(
        "https://synthetic.documents.example:443/",
        Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
        new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
        });

    public void Dispose() => _client.Dispose();

    private QueryDefinition ToCosmosQuery(Expression<Func<NewsOrganisation, bool>> filter) =>
        _client.GetContainer("db", "catalogue").GetItemLinqQueryable<NewsOrganisation>()
            .Where(filter)
            .ToQueryDefinition();

    [Fact(DisplayName =
        "When a news organisation is looked up by YouTube channel, then the Cosmos SQL matches the main indexing.youTubeChannelId or an EXISTS over additionalYouTubeChannels[].youTubeChannelId with the stored camelCase names, because a wrong translation would match no organisation in production while the in-memory rules still pass (#1370).")]
    public void holds_channel_translates_to_main_or_additional_channel()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();

        // Act
        var sql = ToCosmosQuery(NewsOrganisationCosmosFilters.HoldsYouTubeChannel(channelId)).QueryText;

        // Assert
        sql.Should().Contain("root[\"indexing\"][\"youTubeChannelId\"]");
        sql.Should().Contain("EXISTS(");
        sql.Should().Contain("root[\"additionalYouTubeChannels\"]");
        sql.Should().MatchRegex("\\[\"additionalYouTubeChannels\"\\][\\s\\S]*\\[\"youTubeChannelId\"\\]");
        sql.Should().Contain(channelId);
        sql.Should().NotContain("YouTubeChannelId\"]");
        sql.Should().NotContain("AdditionalYouTubeChannels");
        sql.Should().NotContain("Indexing");
    }

    [Fact(DisplayName =
        "When another news organisation is checked for a YouTube channel during an edit, then the Cosmos SQL excludes the edited organisation's id and keeps the main-or-additional channel match with camelCase names, because the uniqueness and channel-wide claim checks must see an additional channel held elsewhere (#1370).")]
    public void other_holds_channel_translates_with_id_exclusion()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var editedId = _fixture.CreateGuid();

        // Act
        var sql = ToCosmosQuery(NewsOrganisationCosmosFilters.OtherHoldsYouTubeChannel(editedId, channelId)).QueryText;

        // Assert
        sql.Should().Contain("root[\"id\"] != ");
        sql.Should().Contain(editedId.ToString());
        sql.Should().Contain("root[\"indexing\"][\"youTubeChannelId\"]");
        sql.Should().Contain("EXISTS(");
        sql.Should().Contain("root[\"additionalYouTubeChannels\"]");
        sql.Should().MatchRegex("\\[\"additionalYouTubeChannels\"\\][\\s\\S]*\\[\"youTubeChannelId\"\\]");
        sql.Should().NotContain("AdditionalYouTubeChannels");
    }

    [Fact(DisplayName =
        "When discovery loads the news organisations that can own an episode, then the Cosmos SQL selects those with indexing or additionalYouTubeChannels by their camelCase names, because an organisation with only additional channels still owns their videos (#1370).")]
    public void has_indexing_or_additional_channels_translates_to_camel_case()
    {
        // Arrange
        // The filter takes no arguments; the client is synthetic.

        // Act
        var sql = ToCosmosQuery(NewsOrganisationCosmosFilters.HasIndexingOrAdditionalYouTubeChannels()).QueryText;

        // Assert
        sql.Should().Contain("root[\"indexing\"]");
        sql.Should().Contain("root[\"additionalYouTubeChannels\"]");
        sql.Should().NotContain("AdditionalYouTubeChannels");
        sql.Should().NotContain("Indexing");
    }
}
