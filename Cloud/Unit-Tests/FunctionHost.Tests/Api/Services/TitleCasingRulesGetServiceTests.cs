using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.TitleCasingRules;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class TitleCasingRulesGetServiceTests
{
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules is asked for a blank language, then the result is NotFound, because an empty code is not a stored language.")]
    public async Task blank_language_is_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync("   ", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.NotFound);
        result.Document.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules finds a stored document, then the result is Ok and not a default, because the repository document is the read model.")]
    public async Task stored_document_is_ok()
    {
        // Arrange
        var stored = new NonEnglishTitleCasingRulesDocument("es");
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(r => r.Get("es"))
            .ReturnsAsync(stored);
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync("ES", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.Ok);
        result.IsDefault.Should().BeFalse();
        result.Document.Should().BeSameAs(stored);
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules has no Universal document, then the result is an empty default, because Universal has no code defaults to materialise.")]
    public async Task missing_universal_is_default()
    {
        // Arrange
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(r => r.Get(TitleCasingRulesDocument.UniversalLanguageKey))
            .ReturnsAsync((TitleCasingRulesDocument?)null);
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync(TitleCasingRulesDocument.UniversalLanguageKey, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.Ok);
        result.IsDefault.Should().BeTrue();
        result.Document.Should().BeOfType<UniversalTitleCasingRulesDocument>();
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules has no English document, then the result is the code default, because the first read must still show the registered English terms.")]
    public async Task missing_english_is_default()
    {
        // Arrange
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(r => r.Get("en"))
            .ReturnsAsync((TitleCasingRulesDocument?)null);
        _mocker.GetMock<ILookupRepository>()
            .Setup(r => r.GetKnownTerms<RedditPodcastPoster.Text.KnownTerms.KnownTerms>())
            .ReturnsAsync((RedditPodcastPoster.Text.KnownTerms.KnownTerms?)null);
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync("en", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.Ok);
        result.IsDefault.Should().BeTrue();
        var english = result.Document.Should().BeOfType<EnglishTitleCasingRulesDocument>().Subject;
        english.LowerCaseTerms.Should().Contain(LowerCaseTerms.DefaultEnglishWords);
    }

    [Fact(DisplayName =
        "Plain English rule: when GET title-casing rules has no non-English document, then the result is NotFound, because only English and Universal materialise a default.")]
    public async Task missing_non_english_is_not_found()
    {
        // Arrange
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(r => r.Get("es"))
            .ReturnsAsync((TitleCasingRulesDocument?)null);
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync("es", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when the title-casing repository throws on GET, then the result is Failed, because a read error is not an empty language.")]
    public async Task repository_failure_is_failed()
    {
        // Arrange
        _mocker.GetMock<ILanguageTitleCasingRulesRepository>()
            .Setup(r => r.Get(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("read failed"));
        var sut = _mocker.CreateInstance<TitleCasingRulesGetService>();

        // Act
        var result = await sut.GetAsync("es", CancellationToken.None);

        // Assert
        result.Status.Should().Be(TitleCasingRulesGetStatus.Failed);
    }
}
