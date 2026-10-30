using System.Text.Json;
using CataloguePlayableServicesBackfill;
using CataloguePlayableServicesBackfill.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.StreamingCatalog;
using Xunit;

namespace CataloguePlayableServicesBackfill.Tests.BusinessRules;

public class PublicDatabaseUrlIndexRules : IDisposable
{
    private readonly DomainTestFixture _fixture = new();
    private readonly string _tempDir;

    static PublicDatabaseUrlIndexRules()
    {
        StreamingCatalogLoader.EnsureLoaded();
    }

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

    [Fact(DisplayName =
        "Public database url index: when leftover urls.bbc holds an iPlayer episode URL, then the indexed key is " +
        "bbcIplayer not bbc, because podcast-shaped public files remap bbc via catalogue URL resolution.")]
    public void remaps_leftover_bbc_key_to_bbc_iplayer_catalogue_key()
    {
        // Arrange — path shape is the assertion dependency (iPlayer → bbcIplayer).
        var episodeId = _fixture.CreateGuid();
        var iplayerPayload = _fixture.CreateGuid().ToString("N")[..8];
        var iplayerUrl = new Uri($"https://www.bbc.co.uk/iplayer/episode/{iplayerPayload}/specimen-title");
        var expectedKey = StreamingServiceWire.ToKey(StreamingService.BbcIplayer);
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
                        ["bbc"] = iplayerUrl.AbsoluteUri
                    }
                }
            }
        });

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir);

        // Assert
        index.TryGet(episodeId, out var hit).Should().BeTrue();
        hit.Urls.Should().ContainKey(expectedKey);
        hit.Urls.Should().NotContainKey("bbc");
        hit.Urls[expectedKey].Should().Be(iplayerUrl);
    }

    [Fact(DisplayName =
        "Public database url index: when a PublicDatabase file is invalid JSON, then that file is skipped with a " +
        "warning and other files still index, because one corrupt file must not silently drop truth.")]
    public void skips_invalid_json_with_warning()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var youtube = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        File.WriteAllText(Path.Combine(_tempDir, "corrupt.json"), "{ not-json");
        WriteJson("film.json", new
        {
            id = filmId,
            name = _fixture.CreateTitle(),
            urls = new Dictionary<string, string>
            {
                [ServiceKeys.YouTube] = youtube.AbsoluteUri
            }
        });
        var logger = new ListLogger();

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir, logger);

        // Assert
        index.TryGet(filmId, out var hit).Should().BeTrue();
        hit.Urls[ServiceKeys.YouTube].Should().Be(youtube);
        logger.Messages.Should().Contain(m => m.Contains("corrupt.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName =
        "Public database url index: when two public files share the same id, then the lexicographically first path " +
        "wins and a warning names both paths, because duplicate exports must be visible and the kept file must not " +
        "depend on directory order.")]
    public void duplicate_id_keeps_first_and_warns()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var firstUrl = _fixture.CreateYouTubeCatalogueInput().YouTubeUrl;
        var secondUrl = _fixture.CreateSpotifyCatalogueInput().SpotifyUrl;
        // a-first.json is written after z-second.json so creation order cannot be what keeps it.
        WriteJson("z-second.json", new
        {
            id,
            name = _fixture.CreateTitle(),
            urls = new Dictionary<string, string>
            {
                [ServiceKeys.Spotify] = secondUrl.AbsoluteUri
            }
        });
        WriteJson("a-first.json", new
        {
            id,
            name = _fixture.CreateTitle(),
            urls = new Dictionary<string, string>
            {
                [ServiceKeys.YouTube] = firstUrl.AbsoluteUri
            }
        });
        var logger = new ListLogger();

        // Act
        var index = FilePublicDatabaseUrlIndex.Load(_tempDir, logger);

        // Assert
        index.TryGet(id, out var hit).Should().BeTrue();
        hit.Urls.Should().ContainKey(ServiceKeys.YouTube);
        hit.Urls.Should().NotContainKey(ServiceKeys.Spotify);
        hit.FilePath.Should().EndWith("a-first.json");
        logger.Messages.Should().Contain(m =>
            m.Contains(id.ToString(), StringComparison.OrdinalIgnoreCase) &&
            m.Contains("a-first.json", StringComparison.OrdinalIgnoreCase) &&
            m.Contains("z-second.json", StringComparison.OrdinalIgnoreCase));
    }

    private void WriteJson(string fileName, object payload)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
    }
}
