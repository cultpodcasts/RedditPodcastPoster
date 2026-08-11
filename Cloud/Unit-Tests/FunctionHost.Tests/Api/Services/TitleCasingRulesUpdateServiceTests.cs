using Api.Models;
using Api.Services.TitleCasingRules;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using Xunit;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace FunctionHost.Tests.Api.Services;

public class TitleCasingRulesUpdateServiceTests
{
    [Fact(DisplayName =
        "Title-casing admin POST known term on Universal: lower-case terms stay empty and siblings are preserved, because Universal only stores known terms.")]
    public async Task upsert_known_term_on_universal_keeps_lower_case_empty()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var existing = new UniversalTitleCasingRulesDocument()
        {
            KnownTerms =
            [
                new KnownTermEntry
                {
                    Literal = "BBC",
                    Pattern = @"\bBBC\b",
                    Options = "IgnoreCase, Compiled"
                }
            ]
        };
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get(TitleCasingRulesDocument.UniversalLanguageKey))
            .ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var lookups = new Mock<ILookupRepository>();
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            lookups.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);

        // Act
        var result = await sut.UpsertKnownTermAsync(
            TitleCasingRulesDocument.UniversalLanguageKey,
            new KnownTermUpdate
            {
                Literal = "NASA",
                Pattern = @"\bNASA\b",
                Options = "IgnoreCase, Compiled"
            },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        saved.Should().NotBeNull();
        saved!.Language.Should().Be(TitleCasingRulesDocument.UniversalLanguageKey);
        saved.Should().BeOfType<UniversalTitleCasingRulesDocument>();
        saved.KnownTerms.Should().HaveCount(2);
        saved.KnownTerms.Should().Contain(t => t.Literal == "BBC");
        saved.KnownTerms.Should().Contain(t => t.Literal == "NASA");
    }

    [Fact(DisplayName =
        "Title-casing admin POST lower-case term for English with no Cosmos document: materialises code defaults then appends, because the first delta must not wipe registered terms shown by GET isDefault.")]
    public async Task add_lower_case_term_on_missing_english_materialises_defaults()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("en")).ReturnsAsync((TitleCasingRulesDocument?)null);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var lookups = new Mock<ILookupRepository>();
        lookups.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            lookups.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);
        var novelTerm = "zz-delta-only-term";

        // Act
        var result = await sut.AddLowerCaseTermAsync(
            "en",
            new TitleCasingRulesAddLowerCaseTermRequest { Term = novelTerm },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        saved.Should().NotBeNull();
        var english = saved.Should().BeOfType<EnglishTitleCasingRulesDocument>().Subject;
        english.LowerCaseTerms.Should().Contain(novelTerm);
        english.LowerCaseTerms.Should().Contain(LowerCaseTerms.DefaultEnglishWords);
        english.LowerCaseTerms.Count.Should().BeGreaterThan(LowerCaseTerms.DefaultEnglishWords.Length);
    }

    [Fact(DisplayName =
        "Title-casing admin DELETE known term: removes only the matching literal and keeps siblings, because deletes are targeted deltas.")]
    public async Task delete_known_term_preserves_siblings()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var existing = new NonEnglishTitleCasingRulesDocument("eo")
        {
            LowerCaseTerms = ["kaj", "la"],
            KnownTerms =
            [
                new KnownTermEntry
                {
                    Literal = "BBC",
                    Pattern = @"\bBBC\b",
                    Options = "IgnoreCase, Compiled"
                },
                new KnownTermEntry
                {
                    Literal = "NASA",
                    Pattern = @"\bNASA\b",
                    Options = "IgnoreCase, Compiled"
                }
            ]
        };
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("eo")).ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var lookups = new Mock<ILookupRepository>();
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            lookups.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);

        // Act
        var result = await sut.DeleteKnownTermAsync("eo", "BBC", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        saved.Should().NotBeNull();
        var nonEnglish = saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.LowerCaseTerms.Should().Equal("kaj", "la");
        nonEnglish.KnownTerms.Should().ContainSingle().Which.Literal.Should().Be("NASA");
    }

    [Fact(DisplayName =
        "Title-casing admin POST ignored subject for English or Universal: rejects with BadRequest, because ignored subjects are only stored on non-English language documents.")]
    public async Task add_ignored_subject_rejects_english_and_universal()
    {
        // Arrange
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        var lookups = new Mock<ILookupRepository>();
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            lookups.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);
        var body = new TitleCasingRulesAddLowerCaseTermRequest { Term = "Hoy" };

        // Act
        var english = await sut.AddIgnoredSubjectAsync("en", body, CancellationToken.None);
        var universal = await sut.AddIgnoredSubjectAsync("*", body, CancellationToken.None);

        // Assert
        english.Status.Should().Be(TitleCasingRulesUpdateStatus.BadRequest);
        universal.Status.Should().Be(TitleCasingRulesUpdateStatus.BadRequest);
        repo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Never);
    }

    [Fact(DisplayName =
        "Title-casing admin POST ignored subject for a non-English language with no Cosmos document: materialises a NonEnglish document then appends the subject.")]
    public async Task add_ignored_subject_on_missing_non_english_materialises_document()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var repo = new Mock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("es")).ReturnsAsync((TitleCasingRulesDocument?)null);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var lookups = new Mock<ILookupRepository>();
        var sut = new TitleCasingRulesUpdateService(
            repo.Object,
            lookups.Object,
            NullLogger<TitleCasingRulesUpdateService>.Instance);

        // Act
        var result = await sut.AddIgnoredSubjectAsync(
            "es",
            new TitleCasingRulesAddLowerCaseTermRequest { Term = "Hoy" },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        var nonEnglish = saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.Language.Should().Be("es");
        nonEnglish.IgnoredSubjects.Should().Equal("Hoy");
    }
}
