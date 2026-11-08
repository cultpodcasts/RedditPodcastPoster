using System.Text.RegularExpressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Subjects.Matching;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.Text.Sanitisers;
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
    private readonly Mock<IPersonService> _personService;
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
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
        _personService = _mocker.GetMock<IPersonService>();
        _personService
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), It.IsAny<bool>()))
            .ReturnsAsync([]);
        _mocker.Use<IEnumerable<IPersonService>>([_personService.Object]);
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

    [Fact(DisplayName =
        "When the lookup carries a podcast descriptionRegex and podcast name, MatchSubjects receives those values " +
        "so Spotify/Apple candidate footers are stripped during YouTube-discovered catalogue classify.")]
    public async Task Description_regex_and_podcast_name_are_passed_to_subject_matcher()
    {
        // Arrange
        SubjectEnrichmentOptions? captured = null;
        _matcher.Setup(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .Callback((Episode _, SubjectEnrichmentOptions? o) => captured ??= o)
            .ReturnsAsync((IList<SubjectMatch>)[]);
        const string regex = @"^(?<body>[\s\S]*?)(?:\r?\n)+\*{5,}[\s\S]*$";
        var lookup = new CatalogueLookup(
            "Synthetic Show",
            "Probe Title",
            null,
            null,
            EnrichingYouTubeDiscoveredEpisode: true,
            DescriptionRegex: regex);

        // Act
        await _sut.ClassifyAsync(new Episode { Title = "Probe Title", Description = "Body\n*****\nFooter" }, [], lookup,
            CancellationToken.None);

        // Assert
        captured.Should().NotBeNull();
        captured!.DescriptionRegex.Should().Be(regex);
        captured.PodcastName.Should().Be("Synthetic Show");
    }

    [Fact(DisplayName =
        "Subjects classified under a different descriptionRegex are not reused from the memo, because stripping " +
        "changes which subject terms are visible in the candidate description.")]
    public async Task Different_description_regex_is_not_shared()
    {
        // Arrange
        var lookupA = new CatalogueLookup("Show", "a", null, null, EnrichingYouTubeDiscoveredEpisode: true,
            DescriptionRegex: @"^(?<body>[\s\S]*?)FOOTER_A[\s\S]*$");
        var lookupB = new CatalogueLookup("Show", "b", null, null, EnrichingYouTubeDiscoveredEpisode: true,
            DescriptionRegex: @"^(?<body>[\s\S]*?)FOOTER_B[\s\S]*$");

        // Act
        await _sut.ClassifyAsync(new Episode { Title = "a" }, Page(), lookupA, CancellationToken.None);
        await _sut.ClassifyAsync(new Episode { Title = "b" }, Page(), lookupB, CancellationToken.None);

        // Assert
        _matcher.Verify(x => x.MatchSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()),
            Times.Exactly(20 + 2));
    }

    [Fact(DisplayName =
        "When People match an alias in the stripped title or description, Episode.Guests receives the canonical " +
        "Person.Name for match-time scoring only (aliases collapse to one person).")]
    public async Task Known_guest_alias_writes_canonical_name_onto_transient_guests()
    {
        // Arrange
        const string canonical = "Gil Duran";
        const string alias = "G. Duran";
        var person = new Person(canonical) { Aliases = [alias] };
        _personService
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), true))
            .ReturnsAsync((Episode e, bool _) =>
                (e.Title.Contains(alias, StringComparison.OrdinalIgnoreCase) ||
                 e.Description.Contains(alias, StringComparison.OrdinalIgnoreCase))
                    ? [new PersonMatch(person, [new PersonMatchResult(alias, 1)])]
                    : []);
        var probe = new Episode
        {
            Title = $"Interview with {alias}",
            Description = "Body text"
        };
        var candidates = new List<Episode>
        {
            new() { Title = "Other", Description = $"Joined by {alias}", ReleaseUtc = DomainTestFixture.UtcDateDaysAgo(1) }
        };

        // Act
        await _sut.ClassifyAsync(probe, candidates, Lookup(probe.Title), CancellationToken.None);

        // Assert
        probe.Guests.Should().Equal(canonical);
        candidates[0].Guests.Should().Equal(canonical);
        _personService.Verify(x => x.MatchEpisode(It.IsAny<Episode>(), true), Times.Exactly(2));
    }

    [Fact(DisplayName =
        "INVARIANT: Two lookups in one scope over the same show page match each candidate's guests once; the second " +
        "lookup reuses the memoised guest names.")]
    public async Task Same_page_guests_matched_once_per_scope()
    {
        // Arrange
        const string canonical = "Gil Duran";
        var person = new Person(canonical);
        _personService
            .Setup(x => x.MatchEpisode(It.IsAny<Episode>(), true))
            .ReturnsAsync([new PersonMatch(person, [new PersonMatchResult(canonical, 1)])]);
        var first = Page();
        var second = Page();

        // Act
        await _sut.ClassifyAsync(new Episode { Title = "a" }, first, Lookup("a"), CancellationToken.None);
        await _sut.ClassifyAsync(new Episode { Title = "b" }, second, Lookup("b"), CancellationToken.None);

        // Assert — 10 candidates once + each lookup's probe
        _personService.Verify(x => x.MatchEpisode(It.IsAny<Episode>(), true), Times.Exactly(10 + 2));
        second.Select(x => x.Guests).Should().BeEquivalentTo(first.Select(x => x.Guests));
    }
}
