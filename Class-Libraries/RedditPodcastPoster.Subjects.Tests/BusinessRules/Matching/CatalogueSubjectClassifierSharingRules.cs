using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.TitleCasing;

namespace RedditPodcastPoster.Subjects.Tests.BusinessRules.Matching;

/// <summary>
/// Candidate subjects are shared across lookups in one scope (one apply/submit batch), from the real case: submission
/// 4a7d8838 had two results from The Compendium, and each lookup re-classified the show's 185 Spotify and 159 Apple
/// episodes (~45 s per platform) although the pages were the same.
/// </summary>
public class CatalogueSubjectClassifierSharingRules
{
    private readonly AutoMocker _mocker = new();
    private readonly Mock<ISubjectMatcher> _matcher;
    private readonly CatalogueSubjectClassifier _sut;

    public CatalogueSubjectClassifierSharingRules()
    {
        _matcher = _mocker.GetMock<ISubjectMatcher>();
        _matcher.Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync((Episode e, SubjectEnrichmentOptions? _) =>
                (IList<SubjectMatch>)[new SubjectMatch(new Subject(e.Title.Contains("Cult") ? "Cults" : "Other"), [])]);
        var rules = _mocker.GetMock<ITitleCasingRulesProvider>();
        rules.Setup(x => x.GetIgnoredSubjectsAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mocker.GetMock<IAsyncInstance<ITitleCasingRulesProvider>>()
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(rules.Object);
        _sut = _mocker.CreateInstance<CatalogueSubjectClassifier>();
    }

    private static List<Episode> Page() =>
        Enumerable.Range(0, 10).Select(i => new Episode
        {
            Title = i == 3 ? "Sex Cult: The Story of Bhagwan Rajneesh" : $"Episode {i}",
            Description = $"Description {i}",
            ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(7 * i)
        }).ToList();

    private static CatalogueLookup Lookup(string title, string? defaultSubject = null) =>
        new("The Compendium", title, null, null, EnrichingYouTubeDiscoveredEpisode: true, DefaultSubject: defaultSubject);

    [Fact(DisplayName =
        "INVARIANT: Two lookups in one scope over the same show page classify each candidate once; the second lookup " +
        "reuses the subjects (fresh Episode objects from the same page still get them).")]
    public async Task Same_page_classified_once_per_scope()
    {
        // Arrange
        var first = Page();
        var second = Page();

        // Act
        await _sut.ClassifyAsync(new Episode { Title = "Heaven's Gate Cult" }, first, Lookup("Heaven's Gate Cult"), CancellationToken.None);
        await _sut.ClassifyAsync(new Episode { Title = "Sex Cult" }, second, Lookup("Sex Cult"), CancellationToken.None);

        // Assert
        _matcher.Verify(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()),
            Times.Exactly(10 + 2), "10 candidates once, plus each lookup's own probe");
        second.Select(x => x.Subjects).Should().BeEquivalentTo(first.Select(x => x.Subjects));
        second[3].Subjects.Should().Equal("Cults");
        second[3].Subjects.Should().NotBeSameAs(first[3].Subjects, "callers may mutate an episode's subjects");
    }

    [Fact(DisplayName =
        "Subjects classified under different options (another default subject or ignore list) are not reused, because " +
        "the options change the result.")]
    public async Task Different_options_are_not_shared()
    {
        // Arrange
        var lookupA = Lookup("a");
        var lookupB = Lookup("b", defaultSubject: "Cults");

        // Act
        await _sut.ClassifyAsync(new Episode { Title = "a" }, Page(), lookupA, CancellationToken.None);
        await _sut.ClassifyAsync(new Episode { Title = "b" }, Page(), lookupB, CancellationToken.None);

        // Assert
        _matcher.Verify(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()),
            Times.Exactly(20 + 2));
    }
}
