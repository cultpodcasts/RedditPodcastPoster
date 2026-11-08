using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules.Matching;

/// <summary>
/// Catalogue classify must strip descriptions with the same DescriptionRegexOptions / per-episode
/// token expansion as posting and SubjectService.Match.
/// </summary>
public class CatalogueSubjectClassifierDescriptionRegexRules
{
    // Website "Remove first line (advert)" preset — needs Singleline so .* reaches \z across lines.
    private const string RemoveFirstLineAdvertPreset = @"^[^\r\n]*\r?\n\s*(?'body'.*)\z";

    // Website "Episode title prefix removal" preset.
    private const string TitlePrefixPreset =
        @"^\s*{episode-title}\s*[-–—:|]?\s*(?<body>.*)$";

    private readonly AutoMocker _mocker = new();
    private readonly CatalogueSubjectClassifier _sut;
    private readonly TextSanitiser _textSanitiser;

    public CatalogueSubjectClassifierDescriptionRegexRules()
    {
        _mocker.GetMock<ISubjectMatcher>()
            .Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync((IList<SubjectMatch>)[]);
        var rules = _mocker.GetMock<ITitleCasingRulesProvider>();
        rules.Setup(x => x.GetIgnoredSubjectsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>()
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules.Object);
        _textSanitiser = new TextSanitiser(
            _mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>().Object,
            Mock.Of<ILogger<TextSanitiser>>());
        _mocker.Use<ITextSanitiser>(_textSanitiser);
        _mocker.GetMock<IPersonService>()
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), It.IsAny<bool>()))
            .ReturnsAsync([]);
        _mocker.Use<IEnumerable<IPersonService>>([_mocker.GetMock<IPersonService>().Object]);
        _sut = _mocker.CreateInstance<CatalogueSubjectClassifier>();
    }

    [Fact(DisplayName =
        "When the podcast uses the Remove-first-line advert DescriptionRegex preset (multi-line .* / \\z), " +
        "catalogue classify strips the advert line the same way TextSanitiser does under Podcast.DescriptionFlags.")]
    public async Task multi_line_dot_star_body_extract_strips_under_classify_like_text_sanitiser()
    {
        // Arrange
        const string advert = "Sponsored by Synthetic Brand";
        const string bodyLine1 = "Today we discuss ritual practice.";
        const string bodyLine2 = "Guests share field notes.";
        var raw = $"{advert}\n{bodyLine1}\n{bodyLine2}";
        var regex = DescriptionRegexTokens.Create(
            RemoveFirstLineAdvertPreset, "Probe Title", "Synthetic Show")!;
        regex.Options.Should().Be(Podcast.DescriptionFlags);
        var expected = _textSanitiser.SanitiseDescription(raw, regex);
        var probe = new Episode { Title = "Probe Title", Description = raw };
        var lookup = new CatalogueLookup(
            "Synthetic Show",
            "Probe Title",
            null,
            null,
            EnrichingYouTubeDiscoveredEpisode: true,
            DescriptionRegex: RemoveFirstLineAdvertPreset);

        // Act
        await _sut.ClassifyAsync(probe, [], lookup, CancellationToken.None);

        // Assert
        expected.Should().Contain(bodyLine1).And.Contain(bodyLine2).And.NotContain(advert);
        probe.Description.Should().Be(expected);
    }

    [Fact(DisplayName =
        "When DescriptionRegex contains {episode-title}, classify expands the token from each episode's own " +
        "title (not the probe lookup title), so a candidate whose description starts with its title is stripped " +
        "and a candidate that does not start with the probe title is left unchanged.")]
    public async Task episode_title_token_expands_per_episode_not_from_probe_lookup_title()
    {
        // Arrange
        const string probeTitle = "Probe Marketing Title";
        const string candidateTitle = "Catalogue Row Title";
        const string probeBody = "Probe body after its own title.";
        const string candidateBody = "Candidate body after its own title.";
        var probe = new Episode
        {
            Title = probeTitle,
            Description = $"{probeTitle} {probeBody}"
        };
        var matchingCandidate = new Episode
        {
            Title = candidateTitle,
            Description = $"{candidateTitle} {candidateBody}"
        };
        var nonMatchingCandidate = new Episode
        {
            Title = "Other Row Title",
            Description = $"{candidateTitle} {candidateBody}"
        };
        var lookup = new CatalogueLookup(
            "Synthetic Show",
            probeTitle,
            null,
            null,
            EnrichingYouTubeDiscoveredEpisode: true,
            DescriptionRegex: TitlePrefixPreset);

        // Act
        await _sut.ClassifyAsync(
            probe,
            [matchingCandidate, nonMatchingCandidate],
            lookup,
            CancellationToken.None);

        // Assert
        probe.Description.Should().Be(probeBody);
        matchingCandidate.Description.Should().Be(candidateBody);
        nonMatchingCandidate.Description.Should().Be($"{candidateTitle} {candidateBody}",
            "must not strip using the probe title against a different row");
    }
}
