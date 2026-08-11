using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using MigrateLanguageIgnoredSubjects;

namespace RedditPodcastPoster.Persistence.Tests;

public class MigrateLanguageIgnoredSubjectsProcessorTests
{
    [Fact(DisplayName =
        "Migrate language ignored subjects: when a non-English language has subjects to seed and no TitleCasingRules document exists, then Save creates a NonEnglish document with those subjects.")]
    public async Task apply_seed_creates_document_when_subjects_present_and_missing_rules()
    {
        // Arrange
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Specimen Show",
            Language = "es",
            IgnoredSubjects = ["Alpha", "Beta"]
        };
        var podcastRepo = new Mock<IPodcastRepository>();
        podcastRepo.Setup(x => x.GetAll()).Returns(ToAsync(podcast));
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.Get("es")).ReturnsAsync((TitleCasingRulesDocument?)null);
        TitleCasingRulesDocument? saved = null;
        titleRepo.Setup(x => x.Save(It.IsAny<TitleCasingRulesDocument>()))
            .Callback<TitleCasingRulesDocument>(d => saved = d)
            .Returns(Task.CompletedTask);
        var sut = CreateSut(podcastRepo, titleRepo);
        var auditPath = NewAuditPath();

        try
        {
            // Act
            var exit = await sut.Run(new MigrateLanguageIgnoredSubjectsRequest
            {
                ApplySeed = true,
                AuditPath = auditPath
            });

            // Assert
            exit.Should().Be(0);
            saved.Should().BeOfType<NonEnglishTitleCasingRulesDocument>();
            ((NonEnglishTitleCasingRulesDocument)saved!).IgnoredSubjects.Should().BeEquivalentTo("Alpha", "Beta");
            titleRepo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Once);
        }
        finally
        {
            DeleteIfExists(auditPath);
        }
    }

    [Fact(DisplayName =
        "Migrate language ignored subjects: when podcast ignored subjects are only whitespace for a language with no TitleCasingRules document, then Save is not called, because an empty TitleCasingRules document must not be created.")]
    public async Task apply_seed_does_not_create_empty_document_when_only_whitespace_subjects()
    {
        // Arrange
        var podcast = new Podcast
        {
            Id = Guid.NewGuid(),
            Name = "Specimen Show",
            Language = "fr",
            IgnoredSubjects = ["  ", "\t"]
        };
        var podcastRepo = new Mock<IPodcastRepository>();
        podcastRepo.Setup(x => x.GetAll()).Returns(ToAsync(podcast));
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        var sut = CreateSut(podcastRepo, titleRepo);
        var auditPath = NewAuditPath();

        try
        {
            // Act
            var exit = await sut.Run(new MigrateLanguageIgnoredSubjectsRequest
            {
                ApplySeed = true,
                AuditPath = auditPath
            });

            // Assert
            exit.Should().Be(0);
            titleRepo.Verify(x => x.Get(It.IsAny<string>()), Times.Never);
            titleRepo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Never);
        }
        finally
        {
            DeleteIfExists(auditPath);
        }
    }

    [Fact(DisplayName =
        "Migrate language ignored subjects: when ApplySeed receives an empty subject list and Get returns null, then Save is skipped, because Cosmos must not gain an empty TitleCasingRules document.")]
    public async Task apply_seed_skips_save_for_empty_seed_without_existing_document()
    {
        // Arrange
        var podcastRepo = new Mock<IPodcastRepository>();
        var titleRepo = new Mock<ILanguageTitleCasingRulesRepository>();
        titleRepo.Setup(x => x.Get("de")).ReturnsAsync((TitleCasingRulesDocument?)null);
        var sut = CreateSut(podcastRepo, titleRepo);
        var auditPath = NewAuditPath();
        var plan = new MigrateLanguageIgnoredSubjectsProcessor.MigrationPlan(
            [new MigrateLanguageIgnoredSubjectsProcessor.LanguageSeed("de", [])],
            []);

        try
        {
            // Act
            var exit = await sut.ApplySeedAsync(plan, auditPath);

            // Assert
            exit.Should().Be(0);
            titleRepo.Verify(x => x.Save(It.IsAny<TitleCasingRulesDocument>()), Times.Never);
            File.Exists(auditPath).Should().BeTrue();
        }
        finally
        {
            DeleteIfExists(auditPath);
        }
    }

    private static MigrateLanguageIgnoredSubjectsProcessor CreateSut(
        Mock<IPodcastRepository> podcastRepo,
        Mock<ILanguageTitleCasingRulesRepository> titleRepo) =>
        new(
            podcastRepo.Object,
            titleRepo.Object,
            NullLogger<MigrateLanguageIgnoredSubjectsProcessor>.Instance);

    private static string NewAuditPath() =>
        Path.Combine(Path.GetTempPath(), "mig-ignored-" + Guid.NewGuid().ToString("N") + ".json");

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static async IAsyncEnumerable<Podcast> ToAsync(params Podcast[] podcasts)
    {
        foreach (var podcast in podcasts)
        {
            yield return podcast;
        }

        await Task.CompletedTask;
    }
}
