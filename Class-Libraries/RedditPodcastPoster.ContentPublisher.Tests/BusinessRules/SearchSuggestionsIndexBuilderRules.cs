using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.ContentPublisher.Builders;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.ContentPublisher.Tests.BusinessRules;

public class SearchSuggestionsIndexBuilderRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Subject[] _subjects = [];
    private Podcast[] _podcasts = [];
    private TvShow[] _tvShows = [];
    private NewsOrganisation[] _newsOrganisations = [];
    private Film[] _films = [];

    public SearchSuggestionsIndexBuilderRules()
    {
        _mocker.Setup<ISubjectRepository, IAsyncEnumerable<Subject>>(r => r.GetAll())
            .Returns(() => _subjects.ToAsyncEnumerable());
        _mocker.Setup<IPodcastRepository, IAsyncEnumerable<Podcast>>(r => r.GetAll())
            .Returns(() => _podcasts.ToAsyncEnumerable());
        _mocker.Setup<ITvShowRepository, IAsyncEnumerable<TvShow>>(r => r.GetAll())
            .Returns(() => _tvShows.ToAsyncEnumerable());
        _mocker.Setup<INewsOrganisationRepository, IAsyncEnumerable<NewsOrganisation>>(r => r.GetAll())
            .Returns(() => _newsOrganisations.ToAsyncEnumerable());
        _mocker.Setup<IFilmRepository, IAsyncEnumerable<Film>>(r => r.GetAll())
            .Returns(() => _films.ToAsyncEnumerable());
    }

    private string CreateName() => $"Name {_fixture.Create<string>()}";

    [Fact(DisplayName =
        "Search suggestions index: when a subject has a name and aliases, then the flat index includes a primary-name row and one row per alias with searchText lowercased, because typeahead matches pre-normalized text.")]
    public void subject_name_and_aliases_emit_flat_rows()
    {
        // Arrange
        var subject = new Subject("Primary Topic") { Aliases = ["PT", "Primary Alias"] };
        var generatedAt = DateTime.UtcNow;

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], [], [], [], [], generatedAt);

        // Assert
        corpus.GeneratedAtUtc.Should().Be(generatedAt);
        corpus.Entries.Should().BeEquivalentTo(
        [
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "primary alias", Alias = (string?)"Primary Alias" },
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "primary topic", Alias = (string?)null },
            new { Type = "subject", Canonical = "Primary Topic", SearchText = "pt", Alias = (string?)"PT" }
        ], options => options.WithStrictOrdering());
    }

    [Fact(DisplayName =
        "Search suggestions index: when a subject has associatedSubjects, then those names are not indexed, because associated subjects are excluded from typeahead.")]
    public void associated_subjects_are_excluded()
    {
        // Arrange
        var subject = new Subject("Primary Topic")
        {
            Aliases = ["PT"],
            AssociatedSubjects = ["Related Topic"]
        };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], [], [], [], []);

        // Assert
        corpus.Entries.Should().NotContain(e =>
            e.SearchText == "related topic" || e.Canonical == "Related Topic");
        corpus.Entries.Should().ContainSingle(e => e.SearchText == "primary topic");
        corpus.Entries.Should().ContainSingle(e => e.SearchText == "pt");
    }

    [Fact(DisplayName =
        "Search suggestions index: when a podcast is not removed, then its name is indexed as a podcast row with lowercased searchText.")]
    public void active_podcast_name_is_indexed()
    {
        // Arrange
        var podcast = new Podcast { Name = "Example Show" };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [podcast], [], [], []);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Type = "podcast",
                Canonical = "Example Show",
                SearchText = "example show",
                Alias = (string?)null
            });
    }

    [Fact(DisplayName =
        "Search suggestions index: when a podcast is marked removed, then it is omitted from the index, because removed shows must not appear in typeahead.")]
    public void removed_podcast_is_omitted()
    {
        // Arrange
        var active = new Podcast { Name = "Active Show" };
        var removed = new Podcast { Name = "Retired Show", Removed = true };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [active, removed], [], [], []);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Canonical.Should().Be("Active Show");
    }

    [Fact(DisplayName =
        "Search suggestions index: when duplicate type+canonical+searchText rows would be emitted, then only one row is kept, because the match key must be unique.")]
    public void duplicate_match_keys_are_deduped()
    {
        // Arrange
        var subject = new Subject("Primary Topic") { Aliases = ["Primary Topic", " primary topic "] };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([subject], [], [], [], []);

        // Assert
        corpus.Entries.Should().ContainSingle(e => e.Type == "subject" && e.SearchText == "primary topic");
    }

    [Fact(DisplayName =
        "Search suggestions index: when a TV show is not removed, then its name is indexed as a tvShow row with no id, and a removed TV show is omitted, because the website links TV shows by name to /tv/{name}.")]
    public void active_tv_show_is_indexed_and_removed_is_omitted()
    {
        // Arrange
        var active = new TvShow(CreateName());
        var removed = new TvShow(CreateName()) { Removed = true };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [], [active, removed], [], []);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Type = "tvShow",
                Canonical = active.Name,
                SearchText = active.Name.ToLowerInvariant(),
                Alias = (string?)null,
                Id = (string?)null
            });
    }

    [Fact(DisplayName =
        "Search suggestions index: when a news organisation is not removed, then its name is indexed as a newsOrganisation row with no id, and a removed one is omitted, because the website links news outlets by name to /news/{name}.")]
    public void active_news_organisation_is_indexed_and_removed_is_omitted()
    {
        // Arrange
        var active = new NewsOrganisation(CreateName());
        var removed = new NewsOrganisation(CreateName()) { Removed = true };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [], [], [active, removed], []);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Type = "newsOrganisation",
                Canonical = active.Name,
                SearchText = active.Name.ToLowerInvariant(),
                Alias = (string?)null,
                Id = (string?)null
            });
    }

    [Fact(DisplayName =
        "Search suggestions index: when a film is neither removed nor ignored, then it is indexed as a film row carrying the film id, and removed or ignored films are omitted, because the website links films to /film/{name}/{shortId}.")]
    public void active_film_is_indexed_with_id_and_removed_or_ignored_are_omitted()
    {
        // Arrange
        var active = new Film(CreateName());
        var removed = new Film(CreateName()) { Removed = true };
        var ignored = new Film(CreateName()) { Ignored = true };

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [], [], [], [active, removed, ignored]);

        // Assert
        corpus.Entries.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                Type = "film",
                Canonical = active.Name,
                SearchText = active.Name.ToLowerInvariant(),
                Alias = (string?)null,
                Id = active.Id.ToString()
            });
    }

    [Fact(DisplayName =
        "Search suggestions index: when two films share a name, then both are indexed with their own id, because film names can repeat and the match key includes the film id.")]
    public void same_name_films_are_not_collapsed()
    {
        // Arrange
        var name = CreateName();
        var first = new Film(name);
        var second = new Film(name);

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build([], [], [], [], [first, second]);

        // Assert
        corpus.Entries.Should().HaveCount(2);
        corpus.Entries.Select(e => e.Id).Should().BeEquivalentTo([first.Id.ToString(), second.Id.ToString()]);
        corpus.Entries.Should().OnlyContain(e => e.Type == "film" && e.Canonical == name);
    }

    [Fact(DisplayName =
        "Search suggestions index: when the same name is a podcast, a TV show, a news organisation and a film, then each kind keeps its own row, because the type is part of the match key.")]
    public void same_name_across_kinds_keeps_one_row_per_kind()
    {
        // Arrange
        var name = CreateName();
        var film = new Film(name);

        // Act
        var corpus = SearchSuggestionsIndexBuilder.Build(
            [], [new Podcast { Name = name }], [new TvShow(name)], [new NewsOrganisation(name)], [film]);

        // Assert
        corpus.Entries.Select(e => e.Type).Should().BeEquivalentTo(["film", "newsOrganisation", "podcast", "tvShow"],
            options => options.WithStrictOrdering());
    }

    [Fact(DisplayName =
        "Search suggestions index: when an entry is serialized, then id is written for film rows and left out for rows without one, because older clients and other kinds never need it.")]
    public void id_is_omitted_from_json_when_null()
    {
        // Arrange
        var film = new Film(CreateName());
        var tvShow = new TvShow(CreateName());
        var corpus = SearchSuggestionsIndexBuilder.Build([], [], [tvShow], [], [film]);

        // Act
        var filmJson = JsonSerializer.Serialize(corpus.Entries.Single(e => e.Type == "film"));
        var tvJson = JsonSerializer.Serialize(corpus.Entries.Single(e => e.Type == "tvShow"));

        // Assert
        filmJson.Should().Contain($"\"id\":\"{film.Id}\"");
        tvJson.Should().NotContain("\"id\"");
    }

    [Fact(DisplayName =
        "Search suggestions index: when the index is built from the repositories, then subjects, podcasts, TV shows, news organisations and films are all read, because every kind belongs in typeahead.")]
    public async Task build_async_reads_every_kind_from_its_repository()
    {
        // Arrange
        _subjects = [new Subject(CreateName())];
        _podcasts = [new Podcast { Name = CreateName() }];
        _tvShows = [new TvShow(CreateName())];
        _newsOrganisations = [new NewsOrganisation(CreateName())];
        _films = [new Film(CreateName())];
        var sut = _mocker.CreateInstance<SearchSuggestionsIndexBuilder>();

        // Act
        var corpus = await sut.BuildAsync();

        // Assert
        corpus.Entries.Select(e => e.Type).Should().BeEquivalentTo(
            ["subject", "podcast", "tvShow", "newsOrganisation", "film"]);
        corpus.Entries.Single(e => e.Type == "film").Id.Should().Be(_films[0].Id.ToString());
    }
}
