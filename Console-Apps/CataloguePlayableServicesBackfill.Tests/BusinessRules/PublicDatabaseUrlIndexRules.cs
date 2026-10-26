using System.Text.Json;
using CataloguePlayableServicesBackfill;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class PublicDatabaseUrlIndexRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly string _tempDir;

    public PublicDatabaseUrlIndexRules()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "cpsb-public-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // best-effort cleanup
        }
    }

    [Fact(DisplayName =
        "Public database url index: when a podcast file nests episodes with urls, then those episode ids are indexed, " +
        "because PublicDatabase stores playable urls under parent documents.")]
    public void indexes_nested_podcast_episode_urls()
    {
        // Arrange
        var episodeId = _fixture.CreateGuid();
        var youtube = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        WriteJson("show.json", new
        {
            id = _fixture.CreateGuid(),
            name = _fixture.CreateTitle(),
            episodes = new[]
            {
                new
                {
                    id = episodeId,
                    title = _fixture.CreateTitle(),
                    urls = new Dictionary<string, string>
                    {
                        [ServiceKeys.YouTube] = youtube.AbsoluteUri
                    }
                }
            }
        });

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir);

        // Assert
        index.TryGet(episodeId, out var hit).Should().BeTrue();
        hit.Urls[ServiceKeys.YouTube].Should().Be(youtube);
        hit.FilePath.Should().EndWith("show.json");
    }

    [Fact(DisplayName =
        "Public database url index: when a film file has root id and urls, then that id is indexed, " +
        "because films are singleton public documents without an episodes array.")]
    public void indexes_film_root_urls()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var apple = _fixture.CreateAppleCatalogueInput().AppleUrl;
        WriteJson("film.json", new
        {
            id = filmId,
            name = _fixture.CreateTitle(),
            urls = new Dictionary<string, string>
            {
                [ServiceKeys.Apple] = apple.AbsoluteUri
            }
        });

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir);

        // Assert
        index.TryGet(filmId, out var hit).Should().BeTrue();
        hit.Urls[ServiceKeys.Apple].Should().Be(apple);
    }

    [Fact(DisplayName =
        "Public database url index: when a news organisation file nests reports with urls, then those report ids are indexed, " +
        "because news public files use a reports array.")]
    public void indexes_nested_news_report_urls()
    {
        // Arrange
        var reportId = _fixture.CreateGuid();
        var youtube = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        WriteJson("org.json", new
        {
            id = _fixture.CreateGuid(),
            name = _fixture.CreateTitle(),
            reports = new[]
            {
                new
                {
                    id = reportId,
                    title = _fixture.CreateTitle(),
                    urls = new Dictionary<string, string>
                    {
                        [ServiceKeys.YouTube] = youtube.AbsoluteUri
                    }
                }
            }
        });

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir);

        // Assert
        index.TryGet(reportId, out var hit).Should().BeTrue();
        hit.Urls[ServiceKeys.YouTube].Should().Be(youtube);
    }

    private void WriteJson(string fileName, object payload)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }
}
