using System.Text.RegularExpressions;
using AutoFixture;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Mapping;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Text.Sanitisers;
using Xunit;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Dtos;

/// <summary>
/// The curator Review page GETs a news report by id; the API maps its post projection through
/// <see cref="EpisodeDtoMapper"/>. The projection's transient podcast carries the news organisation's
/// <c>indexing.titleRegex</c>, which must reach the title sanitiser case-insensitively (#1378).
/// </summary>
public class EpisodeDtoMapperCatalogueTitleRegexRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Regex?> _titleRegexes = [];

    public EpisodeDtoMapperCatalogueTitleRegexRules()
    {
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .Callback((string _, Regex? regex, string[] _, string[] _, string? _) => _titleRegexes.Add(regex))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
    }

    [Fact(DisplayName =
        "Plain English rule: when a news report is mapped for the curator Review page, then its display title is " +
        "sanitised with the news organisation's indexing.titleRegex, case-insensitively, because the outlet suffix " +
        "must be stripped the same way a podcast's title regex strips an episode title (#1378).")]
    public async Task news_report_display_title_uses_organisation_title_regex_ignoring_case()
    {
        // Arrange
        var pattern = $@"^(?'title'.*?)(?'suffix' \| {_fixture.Create<string>()})?$";
        var organisation = new NewsOrganisation(_fixture.Create<string>())
        {
            Id = _fixture.Create<Guid>(),
            Indexing = new CatalogueIndexingSource { TitleRegex = pattern }
        };
        var report = new NewsReport(_fixture.Create<string>()) { Id = _fixture.Create<Guid>() };
        report.SetNewsOrganisationProperties(organisation);
        var projection = CatalogueParentKindMapper.ToPostProjection(report, organisation);
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        await sut.ToDto(projection.Episode, projection.Podcast, Array.Empty<Subject>());

        // Assert
        var regex = _titleRegexes.Should().ContainSingle().Subject;
        regex.Should().NotBeNull();
        regex!.ToString().Should().Be(pattern);
        regex.Options.Should().HaveFlag(RegexOptions.IgnoreCase);
    }
}
