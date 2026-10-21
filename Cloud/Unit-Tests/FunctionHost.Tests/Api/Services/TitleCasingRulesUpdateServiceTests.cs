using Api.Models;
using Api.Services.TitleCasingRules;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using Xunit;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace FunctionHost.Tests.Api.Services;

public class TitleCasingRulesUpdateServiceTests
{
    private readonly AutoMocker _mocker = new();
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
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get(TitleCasingRulesDocument.UniversalLanguageKey))
            .ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

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
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("en")).ReturnsAsync((TitleCasingRulesDocument?)null);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var lookups = _mocker.GetMock<ILookupRepository>();
        lookups.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();
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
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("eo")).ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

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
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();
        var body = new TitleCasingRulesAddIgnoredSubjectRequest { Term = "Hoy" };

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
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("es")).ReturnsAsync((TitleCasingRulesDocument?)null);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.AddIgnoredSubjectAsync(
            "es",
            new TitleCasingRulesAddIgnoredSubjectRequest { Term = "Hoy" },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        var nonEnglish = saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.Language.Should().Be("es");
        nonEnglish.IgnoredSubjects.Should().Equal("Hoy");
    }

    [Fact(DisplayName =
        "Plain English rule: when DELETE lower-case term matches a stored term, then that term is removed and the document is saved, because deletes are targeted deltas.")]
    public async Task delete_lower_case_term_removes_saved_term()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var existing = new NonEnglishTitleCasingRulesDocument("eo")
        {
            LowerCaseTerms = ["kaj", "la"]
        };
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("eo")).ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.DeleteLowerCaseTermAsync("eo", "KAJ", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        var nonEnglish = saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.LowerCaseTerms.Should().Equal("la");
    }

    [Fact(DisplayName =
        "Plain English rule: when DELETE lower-case term names an unknown term, then the result is BadRequest and nothing is saved, because only registered terms can be deleted.")]
    public async Task delete_unknown_lower_case_term_does_not_save()
    {
        // Arrange
        var existing = new NonEnglishTitleCasingRulesDocument("eo")
        {
            LowerCaseTerms = ["kaj"]
        };
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("eo")).ReturnsAsync(existing);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.DeleteLowerCaseTermAsync("eo", "missing", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.BadRequest);
        repo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when DELETE ignored subject removes the last name, then the saved list is null, because an empty ignore list is stored as absent.")]
    public async Task delete_last_ignored_subject_clears_the_list()
    {
        // Arrange
        TitleCasingRulesDocument? saved = null;
        var existing = new NonEnglishTitleCasingRulesDocument("es")
        {
            IgnoredSubjects = ["Hoy"]
        };
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        repo.Setup(x => x.Get("es")).ReturnsAsync(existing);
        repo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.DeleteIgnoredSubjectAsync("es", "Hoy", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Ok);
        var nonEnglish = saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>().Subject;
        nonEnglish.IgnoredSubjects.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when DELETE ignored subject is asked for English, then the result is BadRequest and nothing is saved, because ignored subjects are only stored on non-English documents.")]
    public async Task delete_ignored_subject_rejects_english()
    {
        // Arrange
        var repo = _mocker.GetMock<ILanguageTitleCasingRulesRepository>();
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.DeleteIgnoredSubjectAsync("en", "Hoy", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.BadRequest);
        repo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when the title-casing repository throws on a write, then the result is Failed, because a persistence error is not a validation rejection.")]
    public async Task repository_throw_on_add_lower_case_term_is_failed()
    {
        // Arrange
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(x => x.Get(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("read failed"));
        var sut = _mocker.CreateInstance<TitleCasingRulesUpdateService>();

        // Act
        var result = await sut.AddLowerCaseTermAsync(
            "es",
            new TitleCasingRulesAddLowerCaseTermRequest { Term = "kaj" },
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesUpdateStatus.Failed);
    }
}
