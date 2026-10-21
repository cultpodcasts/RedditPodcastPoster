using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

public class NewsOrganisationCanonicalUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private NewsOrganisation? _saved;
    private NewsReport[]? _savedReports;

    public NewsOrganisationCanonicalUpdateRules()
    {
        _mocker.Use(NullLogger<NewsOrganisationService>.Instance);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation => _saved = organisation)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback<IEnumerable<NewsReport>>(reports => _savedReports = reports.ToArray())
            .Returns(Task.CompletedTask);
    }

    private NewsOrganisation CreateOrganisation(Action<NewsOrganisation>? customise = null)
    {
        var organisation = new NewsOrganisation($"Synthetic {_fixture.CreateTitle()} News")
        {
            Id = _fixture.CreateGuid(),
            TwitterHandle = "@synthetic_news",
            BlueskyHandle = "@synthetic.news.example",
            HashTag = "#SyntheticNews",
            SearchTerms = "synthetic newsroom",
            Language = "fr"
        };
        customise?.Invoke(organisation);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(organisation.Id))
            .ReturnsAsync(organisation);
        return organisation;
    }

    private NewsReport CreateReport(NewsOrganisation organisation)
    {
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        report.SetNewsOrganisationProperties(organisation);
        return report;
    }

    private Task<NewsOrganisationUpdateResult> Update(NewsOrganisation organisation, NewsOrganisationChangeRequest change) =>
        _mocker.CreateInstance<NewsOrganisationService>()
            .UpdateAsync(new NewsOrganisationChangeRequestWrapper(organisation.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH news organisation sets the social handles and hashtags it is given and leaves omitted fields alone, because omitted JSON is a no-op.")]
    public async Task update_sets_given_fields_and_keeps_omitted_ones()
    {
        // Arrange
        var organisation = CreateOrganisation();

        // Act
        var result = await Update(organisation, new NewsOrganisationChangeRequest
        {
            TwitterHandle = " @synthetic_desk ",
            EnrichmentHashTags = [" #Cults ", "#Raids"],
            KnownTerms = ["Synthetic Compound"],
            DefaultSubject = "Synthetic Subject",
            IgnoredSubjects = ["Weather"]
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _saved.Should().BeSameAs(organisation);
        _saved!.TwitterHandle.Should().Be("@synthetic_desk");
        _saved.EnrichmentHashTags.Should().Equal("#Cults", "#Raids");
        _saved.KnownTerms.Should().Equal("Synthetic Compound");
        _saved.DefaultSubject.Should().Be("Synthetic Subject");
        _saved.IgnoredSubjects.Should().Equal("Weather");
        _saved.BlueskyHandle.Should().Be("@synthetic.news.example");
        _saved.HashTag.Should().Be("#SyntheticNews");
        _saved.SearchTerms.Should().Be("synthetic newsroom");
    }

    [Fact(DisplayName =
        "PATCH news organisation with empty strings and empty arrays clears those fields, because that is how a curator removes a value.")]
    public async Task update_clears_with_empty_values()
    {
        // Arrange
        var organisation = CreateOrganisation(o =>
        {
            o.EnrichmentHashTags = ["#Old"];
            o.KnownTerms = ["Old"];
            o.DefaultSubject = "Old";
        });

        // Act
        var result = await Update(organisation, new NewsOrganisationChangeRequest
        {
            BlueskyHandle = "",
            HashTag = "",
            EnrichmentHashTags = [],
            KnownTerms = [],
            DefaultSubject = ""
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _saved!.BlueskyHandle.Should().BeNull();
        _saved.HashTag.Should().BeNull();
        _saved.EnrichmentHashTags.Should().BeNull();
        _saved.KnownTerms.Should().BeNull();
        _saved.DefaultSubject.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation with new search terms and language saves them onto its reports, because search reads the organisation's terms and language from each report.")]
    public async Task update_propagates_search_terms_and_language_to_reports()
    {
        // Arrange
        var organisation = CreateOrganisation();
        var first = CreateReport(organisation);
        var second = CreateReport(organisation);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(organisation.Id))
            .Returns(() => new[] { first, second }.ToAsyncEnumerable());

        // Act
        var result = await Update(organisation, new NewsOrganisationChangeRequest
        {
            SearchTerms = "synthetic bulletin", Language = "de"
        });

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.Accepted);
        _savedReports.Should().BeEquivalentTo(new[] { first, second }, o => o.WithoutStrictOrdering());
        _savedReports!.Should().OnlyContain(r => r.PublisherSearchTerms == "synthetic bulletin" && r.PublisherLanguage == "de");
    }

    [Fact(DisplayName =
        "PATCH news organisation that changes neither search terms nor language does not read its reports, because a large organisation's reports should not be loaded for a handle change.")]
    public async Task update_without_search_fields_skips_reports()
    {
        // Arrange
        var organisation = CreateOrganisation();

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest { TwitterHandle = "@synthetic_other" });

        // Assert
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()), Times.Never);
        _savedReports.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation with English as the language stores null, because null means English for every kind.")]
    public async Task update_normalises_english_language()
    {
        // Arrange
        var organisation = CreateOrganisation();
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetByNewsOrganisationId(organisation.Id))
            .Returns(() => AsyncEnumerable.Empty<NewsReport>());

        // Act
        await Update(organisation, new NewsOrganisationChangeRequest { Language = "en-GB" });

        // Assert
        _saved!.Language.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH news organisation that does not exist returns NotFound and saves nothing, because there is nothing to edit.")]
    public async Task update_unknown_returns_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<NewsOrganisationService>();

        // Act
        var result = await sut.UpdateAsync(
            new NewsOrganisationChangeRequestWrapper(_fixture.CreateGuid(), new NewsOrganisationChangeRequest()),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationUpdateStatus.NotFound);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "GET news organisation by id returns Found with its editable fields, because curator UIs load the document before editing.")]
    public async Task get_by_id_returns_the_organisation()
    {
        // Arrange
        var organisation = CreateOrganisation();
        var sut = _mocker.CreateInstance<NewsOrganisationService>();

        // Act
        var result = await sut.GetAsync(organisation.Id.ToString(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationGetStatus.Found);
        var dto = result.NewsOrganisation!.ToDto();
        dto.Id.Should().Be(organisation.Id);
        dto.Name.Should().Be(organisation.Name);
        dto.TwitterHandle.Should().Be("@synthetic_news");
        dto.SearchTerms.Should().Be("synthetic newsroom");
        dto.Removed.Should().BeFalse();
    }

    [Fact(DisplayName =
        "GET news organisation by a name shared by two organisations returns Conflict with both ids, because the curator has to choose.")]
    public async Task get_by_ambiguous_name_returns_conflict()
    {
        // Arrange
        const string name = "Synthetic Evening News";
        var first = new NewsOrganisation(name) { Id = _fixture.CreateGuid() };
        var second = new NewsOrganisation(name) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => new[] { first, second }.ToAsyncEnumerable());
        var sut = _mocker.CreateInstance<NewsOrganisationService>();

        // Act
        var result = await sut.GetAsync(name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsOrganisationGetStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
    }
}
