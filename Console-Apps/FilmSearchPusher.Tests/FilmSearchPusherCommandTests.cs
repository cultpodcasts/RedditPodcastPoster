using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.FilmSearchPush;

namespace FilmSearchPusher.Tests;

public class FilmSearchPusherCommandTests
{
    private readonly Fixture _fixture = new();

    [Fact(DisplayName =
        "When the console options are mapped, then --full, --dry-run and --batch-size reach the push request unchanged, because the console is a thin host over the shared push service.")]
    public void options_map_to_push_request()
    {
        // Arrange
        var request = new FilmSearchPusherRequest
        {
            Full = _fixture.Create<bool>(), DryRun = _fixture.Create<bool>(), BatchSize = _fixture.Create<int>()
        };

        // Act
        var mapped = FilmSearchPusherCommand.ToPushRequest(request);

        // Assert
        mapped.Should().Be(new FilmSearchPushRequest(request.Full, request.DryRun, request.BatchSize));
    }

    [Fact(DisplayName = "When --id is given, then the push request carries that film id.")]
    public void id_option_maps_to_film_id()
    {
        // Arrange
        var request = new FilmSearchPusherRequest { Id = _fixture.Create<Guid>() };

        // Act
        var mapped = FilmSearchPusherCommand.ToPushRequest(request);

        // Assert
        mapped.FilmId.Should().Be(request.Id);
    }

    [Theory(DisplayName =
        "When a push finishes, then the exit code is 0 on success and 1 on failure, so a scheduled task can tell a failed run.")]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public void exit_code_reflects_success(bool succeeded, int expected)
    {
        // Arrange
        var result = new FilmSearchPushResult(succeeded, FullPush: false, DryRun: false, PreviousWatermark: null,
            NewWatermark: null, FilmsRead: 0, ToPush: 0, ToDelete: 0, Failed: succeeded ? 0 : 1, WatermarkSaved: succeeded);

        // Act
        var exitCode = FilmSearchPusherCommand.ExitCode(result);

        // Assert
        exitCode.Should().Be(expected);
    }
}
