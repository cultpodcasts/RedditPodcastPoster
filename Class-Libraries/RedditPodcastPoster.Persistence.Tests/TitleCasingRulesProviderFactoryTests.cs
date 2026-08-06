using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.Lookups;
using RedditPodcastPoster.Text.TitleCasing;
using KnownTermsModel = RedditPodcastPoster.Text.KnownTerms.KnownTerms;

namespace RedditPodcastPoster.Persistence.Tests;

public class TitleCasingRulesProviderFactoryTests
{
    [Fact(DisplayName =
        "When TitleCasingRules container has no en document, provider seeds in-memory English defaults.")]
    public async Task Create_WithMissingEnglish_SeedsInMemoryDefaults()
    {
        // Arrange
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.GetAll()).Returns(EmptyDocuments());
        var lookup = new Mock<ILookupRepository>();
        lookup.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = new TitleCasingRulesProviderFactory(
            titleRepo.Object,
            lookup.Object,
            NullLogger<TitleCasingRulesProviderFactory>.Instance);

        // Act
        var provider = await sut.Create();

        // Assert
        provider.GetAll().Should().ContainKey("en");
        provider.GetLowerCaseExpressions("en").Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "When TitleCasingRules container has a language document, provider uses those lower-case terms.")]
    public async Task Create_WithPersistedLanguage_UsesPersistedTerms()
    {
        // Arrange
        var document = new LanguageTitleCasingRulesDocument("fr")
        {
            LowerCaseTerms = ["de", "la"],
            KnownTerms = []
        };
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.GetAll()).Returns(SingleDocument(document));
        var lookup = new Mock<ILookupRepository>();
        lookup.Setup(x => x.GetKnownTerms<KnownTermsModel>())
            .ReturnsAsync((KnownTermsModel?)null);
        var sut = new TitleCasingRulesProviderFactory(
            titleRepo.Object,
            lookup.Object,
            NullLogger<TitleCasingRulesProviderFactory>.Instance);

        // Act
        var provider = await sut.Create();

        // Assert
        provider.GetAll().Should().ContainKey("fr");
        provider.GetAll().Should().ContainKey("en");
        provider.GetLowerCaseExpressions("fr").Keys.Should().Contain("de");
    }

    private static async IAsyncEnumerable<LanguageTitleCasingRulesDocument> EmptyDocuments()
    {
        yield break;
    }

    private static async IAsyncEnumerable<LanguageTitleCasingRulesDocument> SingleDocument(
        LanguageTitleCasingRulesDocument document)
    {
        yield return document;
        await Task.CompletedTask;
    }
}
