using FluentAssertions;

namespace CosmosDbDownloader.Tests.BusinessRules;

public class DownloadContainerSelectionRules
{
    [Fact(DisplayName =
        "Cosmos dump: when no --only or --skip is set, then Film, TvShow, TvShowEpisode, " +
        "NewsOrganisation, and NewsReport containers are included, because GATE 5 backups " +
        "must cover the new catalogue families.")]
    public void default_all_includes_catalogue_containers()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest();

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.Films.Should().BeTrue();
        selection.TvShows.Should().BeTrue();
        selection.TvShowEpisodes.Should().BeTrue();
        selection.NewsOrganisations.Should().BeTrue();
        selection.NewsReports.Should().BeTrue();
        selection.EnabledNames.Should().Contain(
        [
            DownloadContainerSelection.FilmsName,
            DownloadContainerSelection.TvShowsName,
            DownloadContainerSelection.TvShowEpisodesName,
            DownloadContainerSelection.NewsOrganisationsName,
            DownloadContainerSelection.NewsReportsName
        ]);
    }

    [Fact(DisplayName =
        "Cosmos dump: when --only names a catalogue container, then only that family is selected, " +
        "because operators can dump Film/TV/News without re-pulling episodes.")]
    public void only_films_selects_films_alone()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Only = ["film"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.Films.Should().BeTrue();
        selection.Podcasts.Should().BeFalse();
        selection.Episodes.Should().BeFalse();
        selection.EnabledNames.Should().Equal(DownloadContainerSelection.FilmsName);
    }

    [Fact(DisplayName =
        "Cosmos dump: when --skip names news organisations, then news reports still download, " +
        "because skip is per container name not a whole product family.")]
    public void skip_news_organisations_keeps_news_reports()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Skip = ["newsorganisations"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.NewsOrganisations.Should().BeFalse();
        selection.NewsReports.Should().BeTrue();
    }
}
