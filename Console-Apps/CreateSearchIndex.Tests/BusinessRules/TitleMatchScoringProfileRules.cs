using System.Text.Json;
using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using CreateSearchIndex;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Persistence.Configuration;
using RedditPodcastPoster.Search.Indexing;
using RedditPodcastPoster.Search.Models;

namespace CreateSearchIndex.Tests;

/// <summary>
/// RPP #1434: an exact alternative-title match ranks with an exact title match.
/// Fixtures: Faderhuset / "Father's House" and Going Clear / "Going Clear: Scientology and the Prison of Belief".
/// </summary>
public class TitleMatchScoringProfileRules
{
    private const string IndexName = "synthetic-index";

    /// <summary>Faderhuset (50f1a623): a plain search for "Father's House" should rank this film with a title match.</summary>
    private static readonly AlternativeTitleFixture Faderhuset = new(
        "50f1a623",
        "Faderhuset",
        "Father's House",
        ["Father's House", "La casa del padre"]);

    /// <summary>Going Clear: a search for the long name should rank the film with a title match.</summary>
    private static readonly AlternativeTitleFixture GoingClear = new(
        null,
        "Going Clear",
        "Going Clear: Scientology and the Prison of Belief",
        ["Going Clear: Scientology and the Prison of Belief"]);

    [Theory(DisplayName =
        "The scoring profile gives alternativeTitles the same weight as title, so an exact match on a film's other name ranks with an exact title match.")]
    [MemberData(nameof(AlternativeTitleFilms))]
    public void alternative_titles_weigh_the_same_as_title(AlternativeTitleFixture film)
    {
        // Arrange
        film.AlternativeTitles.Should().Contain(film.Query);
        film.Title.Should().NotBe(film.Query);
        var fields = UnifiedFields();

        // Act
        var weights = TitleMatchScoringProfile.WeightsFor(fields);

        // Assert
        weights[TitleMatchScoringProfile.TitleField].Should().Be(TitleMatchScoringProfile.Weight);
        weights[TitleMatchScoringProfile.AlternativeTitlesField].Should().Be(TitleMatchScoringProfile.Weight);
        weights[TitleMatchScoringProfile.SeriesNameField].Should().Be(TitleMatchScoringProfile.Weight);
        weights[TitleMatchScoringProfile.TitleField].Should()
            .Be(weights[TitleMatchScoringProfile.AlternativeTitlesField])
            .And.Be(weights[TitleMatchScoringProfile.SeriesNameField]);
        weights.ContainsKey(TitleMatchScoringProfile.EpisodeTitleField).Should().BeFalse();
        weights.ContainsKey(TitleMatchScoringProfile.PodcastNameField).Should().BeFalse();
    }

    public static TheoryData<AlternativeTitleFixture> AlternativeTitleFilms() => [Faderhuset, GoingClear];

    [Fact(DisplayName =
        "When the live index still has episodeTitle as well as title, then only title and alternativeTitles are boosted, so a document that carries both title fields is not scored twice.")]
    public void hybrid_index_does_not_boost_legacy_episode_title()
    {
        // Arrange
        var fields = UnifiedFields();
        fields.Add(new SearchableField(TitleMatchScoringProfile.EpisodeTitleField));

        // Act
        var weights = TitleMatchScoringProfile.WeightsFor(fields);

        // Assert
        weights.Keys.Should().BeEquivalentTo(
            TitleMatchScoringProfile.TitleField,
            TitleMatchScoringProfile.AlternativeTitlesField,
            TitleMatchScoringProfile.SeriesNameField);
        weights[TitleMatchScoringProfile.TitleField].Should()
            .Be(weights[TitleMatchScoringProfile.AlternativeTitlesField]);
    }

    [Fact(DisplayName =
        "When the index has only the legacy episodeTitle field, then episodeTitle and alternativeTitles have the same weight.")]
    public void legacy_index_boosts_episode_title_with_alternative_titles()
    {
        // Arrange
        var fields = new List<SearchField>
        {
            new SearchableField(TitleMatchScoringProfile.EpisodeTitleField),
            new SearchableField(TitleMatchScoringProfile.AlternativeTitlesField, collection: true)
        };

        // Act
        var weights = TitleMatchScoringProfile.WeightsFor(fields);

        // Assert
        weights[TitleMatchScoringProfile.EpisodeTitleField].Should()
            .Be(weights[TitleMatchScoringProfile.AlternativeTitlesField])
            .And.Be(TitleMatchScoringProfile.Weight);
        weights.ContainsKey(TitleMatchScoringProfile.TitleField).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the index has only the legacy podcastName field, then podcastName has the same weight as alternativeTitles, and a hybrid index with seriesName does not also boost podcastName.")]
    public void series_name_and_legacy_podcast_name_weights()
    {
        var legacy = new List<SearchField>
        {
            new SearchableField(TitleMatchScoringProfile.TitleField),
            new SearchableField(TitleMatchScoringProfile.AlternativeTitlesField, collection: true),
            new SearchableField(TitleMatchScoringProfile.PodcastNameField)
        };
        var hybrid = UnifiedFields();
        hybrid.Add(new SearchableField(TitleMatchScoringProfile.PodcastNameField));

        var legacyWeights = TitleMatchScoringProfile.WeightsFor(legacy);
        var hybridWeights = TitleMatchScoringProfile.WeightsFor(hybrid);

        legacyWeights[TitleMatchScoringProfile.PodcastNameField].Should()
            .Be(legacyWeights[TitleMatchScoringProfile.AlternativeTitlesField])
            .And.Be(TitleMatchScoringProfile.Weight);
        legacyWeights.ContainsKey(TitleMatchScoringProfile.SeriesNameField).Should().BeFalse();
        hybridWeights[TitleMatchScoringProfile.SeriesNameField].Should().Be(TitleMatchScoringProfile.Weight);
        hybridWeights.ContainsKey(TitleMatchScoringProfile.PodcastNameField).Should().BeFalse();
    }

    [Fact(DisplayName =
        "Applying the profile adds it as a named profile, does not make it the index default, and leaves existing field names and analyzers unchanged.")]
    public void apply_adds_named_profile_without_setting_default_or_changing_fields()
    {
        // Arrange
        var index = new SearchIndex(IndexName) { Fields = UnifiedFields() };
        var fieldSnapshot = index.Fields.Select(FieldSignature).ToArray();
        var previousDefault = index.DefaultScoringProfile;

        // Act
        var changed = TitleMatchScoringProfile.Apply(index);

        // Assert
        changed.Should().BeTrue();
        index.DefaultScoringProfile.Should().Be(previousDefault);
        index.DefaultScoringProfile.Should().NotBe(TitleMatchScoringProfile.Name);
        var profile = index.ScoringProfiles.Should().ContainSingle(p => p.Name == TitleMatchScoringProfile.Name).Subject;
        profile.TextWeights.Weights[TitleMatchScoringProfile.TitleField]
            .Should().Be(profile.TextWeights.Weights[TitleMatchScoringProfile.AlternativeTitlesField])
            .And.Be(profile.TextWeights.Weights[TitleMatchScoringProfile.SeriesNameField]);
        index.Fields.Select(FieldSignature).Should().Equal(fieldSnapshot);
        TitleMatchScoringProfile.Apply(index).Should().BeFalse();
    }

    [Fact(DisplayName =
        "When --update-existing runs on an index that already has every field, then the title-match scoring profile is added but not set as the default, the existing fields are left as they were, and the index is not torn down or reset.")]
    public async Task update_existing_adds_scoring_profile_without_reset_or_teardown()
    {
        // Arrange
        var mocker = ProcessorMocker();
        var live = new SearchIndex(IndexName) { Fields = EpisodeSearchFields() };
        var fieldSnapshot = live.Fields.Select(FieldSignature).ToArray();
        SearchIndex? updated = null;
        mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetIndexAsync(IndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(live, Status(200)));
        mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback((SearchIndex index, bool _, bool _, CancellationToken _) => updated = index)
            .ReturnsAsync((SearchIndex index, bool _, bool _, CancellationToken _) =>
                Response.FromValue(index, Status(200)));
        var sut = mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(new CreateSearchIndexRequest
        {
            IndexName = IndexName, DataSourceName = $"{IndexName}-ds", UpdateExisting = true
        });

        // Assert
        updated.Should().NotBeNull();
        updated!.DefaultScoringProfile.Should().NotBe(TitleMatchScoringProfile.Name);
        var profile = updated.ScoringProfiles.Should()
            .ContainSingle(p => p.Name == TitleMatchScoringProfile.Name).Subject;
        profile.TextWeights.Weights[TitleMatchScoringProfile.TitleField].Should()
            .Be(profile.TextWeights.Weights[TitleMatchScoringProfile.AlternativeTitlesField])
            .And.Be(profile.TextWeights.Weights[TitleMatchScoringProfile.SeriesNameField])
            .And.Be(TitleMatchScoringProfile.Weight);
        updated.Fields.Select(FieldSignature).Should().Equal(fieldSnapshot);
        mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mocker.GetMock<SearchIndexerClient>()
            .Verify(x => x.ResetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When --update-existing runs on an index that already has the title-match scoring profile, then the index is not written again.")]
    public async Task update_existing_is_idempotent_when_profile_already_applied()
    {
        // Arrange
        var mocker = ProcessorMocker();
        var live = new SearchIndex(IndexName) { Fields = EpisodeSearchFields() };
        TitleMatchScoringProfile.Apply(live).Should().BeTrue();
        mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetIndexAsync(IndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(live, Status(200)));
        var sut = mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(new CreateSearchIndexRequest
        {
            IndexName = IndexName, DataSourceName = $"{IndexName}-ds", UpdateExisting = true
        });

        // Assert
        mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()), Times.Never);
    }

    public readonly record struct AlternativeTitleFixture(
        string? Id,
        string Title,
        string Query,
        IReadOnlyList<string> AlternativeTitles);

    private static List<SearchField> UnifiedFields() =>
    [
        new SearchableField(TitleMatchScoringProfile.TitleField),
        new SearchableField(TitleMatchScoringProfile.AlternativeTitlesField, collection: true),
        new SearchableField(TitleMatchScoringProfile.SeriesNameField),
        new SearchableField("description")
    ];

    private static IList<SearchField> EpisodeSearchFields() => new FieldBuilder
    {
        Serializer = new JsonObjectSerializer(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        })
    }.Build(typeof(EpisodeSearchRecord));

    private static string FieldSignature(SearchField field) =>
        $"{field.Name}|{field.IsSearchable}|{field.IsFilterable}|{field.IsFacetable}|{field.IsSortable}|{field.IsKey}|{field.Type}|{field.AnalyzerName}";

    private static AutoMocker ProcessorMocker()
    {
        var mocker = new AutoMocker();
        mocker.Use(Options.Create(new CosmosDbSettings
        {
            Endpoint = "https://synthetic.documents.example/", AuthKeyOrResourceToken = "synthetic",
            DatabaseId = "db", PodcastsContainer = "podcasts", EpisodesContainer = "episodes",
            TvShowsContainer = "tvshows", TvShowEpisodesContainer = "tvshowepisodes", FilmsContainer = "films",
            NewsOrganisationsContainer = "newsorganisations", NewsReportsContainer = "newsreports",
            SubjectsContainer = "subjects", PeopleContainer = "people", ActivitiesContainer = "activities",
            DiscoveryContainer = "discovery", DiscoverySubmissionsContainer = "submissions",
            LookUpsContainer = "lookups", FileKeyClaimsContainer = "filekeyclaims",
            TitleCasingRulesContainer = "titlecasing",
            PushSubscriptionsContainer = "push"
        }));
        mocker.Use(Mock.Of<Azure.Search.Documents.SearchClient>());
        mocker.Use(Mock.Get(Mock.Of<SearchIndexerClient>()));
        mocker.Use(Mock.Get(Mock.Of<SearchIndexClient>()));
        var indexer = mocker.GetMock<SearchIndexerClient>();
        indexer.Setup(x => x.GetDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => Response.FromValue(
                new SearchIndexerDataSourceConnection(name, SearchIndexerDataSourceType.CosmosDb, "cs",
                    new SearchIndexerDataContainer("c")), Status(200)));
        indexer.Setup(x => x.CreateOrUpdateDataSourceConnectionAsync(It.IsAny<SearchIndexerDataSourceConnection>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchIndexerDataSourceConnection d, bool _, CancellationToken _) =>
                Response.FromValue(d, Status(200)));
        return mocker;
    }

    private static Response Status(int status) => new StatusResponse(status);

    private sealed class StatusResponse(int status) : Response
    {
        public override int Status => status;
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;
        public override void Dispose() { }
        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<Azure.Core.HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }
}
