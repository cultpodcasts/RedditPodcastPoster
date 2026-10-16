using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Search.Querying;
using RemoveEpisodes.Tests.Fakes;

namespace RemoveEpisodes.Tests.BusinessRules;

public class EpisodeSearchOptionsRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly FakeEpisodeSearchIndex _index = new();

    [Fact(DisplayName =
        "Episode search options: when the fields the search selects are compared with the fields the search document " +
        "reads, then every selected field is one the search document maps, because a selected field the document " +
        "does not map is returned and then ignored.")]
    public void every_selected_field_is_read_by_the_search_document()
    {
        // Arrange
        var mappedFields = typeof(SearchDocument)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name)
            .OfType<string>()
            .ToArray();

        // Act
        var selected = EpisodeSearchOptions.Create().Select.ToArray();

        // Assert
        selected.Should().BeEquivalentTo(EpisodeSearchOptions.SelectedFields);
        mappedFields.Should().Contain(selected);
    }

    [Fact(DisplayName =
        "Episode search options: when an episode is read back from the index with the selected fields, then the " +
        "search document carries its id, series name, title, description and subjects, because the console tools " +
        "act on these and a field missing from the select would read as null.")]
    public async Task selected_fields_populate_what_the_console_tools_read()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var seriesName = _fixture.CreateTitle();
        var title = _fixture.CreateTitle();
        var description = _fixture.CreateTitle();
        var subject = _fixture.CreateTitle();
        _index.Add(SearchContentKind.Episode, id, title, description, seriesName, subject);

        // Act
        var response = await _index.Search(EpisodeSearchOptions.Create());
        var results = await response.Value.GetResultsAsync().ToListAsync();

        // Assert
        var document = results.Should().ContainSingle().Which.Document;
        document.Id.Should().Be(id);
        document.SeriesName.Should().Be(seriesName);
        document.Title.Should().Be(title);
        document.Description.Should().Be(description);
        document.Subjects.Should().Equal(subject);
    }
}