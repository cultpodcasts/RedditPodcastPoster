using FluentAssertions;

namespace CosmosDbDownloader.Tests.BusinessRules;

public class DownloadContainerSelectionRules
{
    [Fact(DisplayName =
        "Cosmos dump: when no --only or --skip is set, then every downloader container is selected " +
        "(including Film, TvShow, TvShowEpisode, NewsOrganisation, and NewsReport), because GATE 5 " +
        "backups must match production.")]
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
        selection.EnabledNames.Should().BeEquivalentTo(DownloadContainerSelection.AllNames);
    }

    [Fact(DisplayName =
        "Cosmos dump: when --only names film, then only the films container is selected, " +
        "because Film is a one-off with no parent catalogue.")]
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
        "Cosmos dump: when --only names the tv family alias, then tvshows and tvshowepisodes " +
        "are selected together, because TV is parent plus playable.")]
    public void only_tv_selects_tv_shows_and_episodes()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Only = ["tv"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.EnabledNames.Should().BeEquivalentTo(
        [
            DownloadContainerSelection.TvShowsName,
            DownloadContainerSelection.TvShowEpisodesName
        ]);
    }

    [Fact(DisplayName =
        "Cosmos dump: when --only names the news family alias, then newsorganisations and " +
        "newsreports are selected together, because News is parent plus playable.")]
    public void only_news_selects_news_organisations_and_reports()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Only = ["news"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.EnabledNames.Should().BeEquivalentTo(
        [
            DownloadContainerSelection.NewsOrganisationsName,
            DownloadContainerSelection.NewsReportsName
        ]);
    }

    [Fact(DisplayName =
        "Cosmos dump: when --only names a per-container TV alias, then only tvshows is selected, " +
        "because tvshow is not a family alias.")]
    public void only_tvshow_selects_tv_shows_alone()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Only = ["tvshow"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.EnabledNames.Should().Equal(DownloadContainerSelection.TvShowsName);
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

    [Fact(DisplayName =
        "Cosmos dump: when --skip names the news family alias, then news organisations and " +
        "news reports are both omitted, because family aliases apply to every container in that family.")]
    public void skip_news_omits_news_organisations_and_reports()
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Skip = ["news"] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.NewsOrganisations.Should().BeFalse();
        selection.NewsReports.Should().BeFalse();
        selection.EnabledNames.Should().NotContain(DownloadContainerSelection.NewsOrganisationsName);
        selection.EnabledNames.Should().NotContain(DownloadContainerSelection.NewsReportsName);
    }

    public static TheoryData<string> CanonicalContainerNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in DownloadContainerSelection.AllNames)
        {
            data.Add(name);
        }

        return data;
    }

    [Theory(DisplayName =
        "Cosmos dump: when --only names a canonical container from the catalog, then only that " +
        "container is selected, because FromRequest is derived from one catalog not a boolean bag.")]
    [MemberData(nameof(CanonicalContainerNames))]
    public void only_canonical_name_selects_that_container_alone(string canonicalName)
    {
        // Arrange
        var request = new CosmosDbDownloaderRequest { Only = [canonicalName] };

        // Act
        var selection = DownloadContainerSelection.FromRequest(request);

        // Assert
        selection.EnabledNames.Should().Equal(canonicalName);
        selection.Includes(canonicalName).Should().BeTrue();
    }
}
