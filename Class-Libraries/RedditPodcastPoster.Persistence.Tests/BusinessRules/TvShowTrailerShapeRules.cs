using System.Reflection;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class TvShowTrailerShapeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "TvShowTrailer and FilmTrailer expose the same public properties, with the same JSON names, types, order and null handling, because a trailer property added to one and not the other would silently drop from the other kind's trailers (RPP #1412).")]
    public void tv_show_trailer_and_film_trailer_have_the_same_shape()
    {
        // Arrange
        // (no arrangement: the shapes are read from the types)

        // Act
        var film = ShapeOf(typeof(FilmTrailer));
        var tv = ShapeOf(typeof(TvShowTrailer));

        // Assert
        film.Should().NotBeEmpty();
        tv.Should().Equal(film);
    }

    [Fact(DisplayName =
        "Adopting trailers from a source show appends the ones the show does not hold, keeping the show's own trailers first and the source's order after, because a merged show must not lose a trailer (RPP #1412).")]
    public void adopting_trailers_appends_the_missing_ones_in_order()
    {
        // Arrange
        var own = CreateTrailer();
        var first = CreateTrailer();
        var second = CreateTrailer();
        var show = new TvShow(_fixture.CreateTitle()) { Trailers = [own] };
        var source = new TvShow(_fixture.CreateTitle()) { Trailers = [first, second] };

        // Act
        var adopted = show.AdoptTrailersFrom(source);

        // Assert
        adopted.Select(trailer => trailer.Id).Should().Equal(first.Id, second.Id);
        show.Trailers!.Select(trailer => trailer.Id).Should().Equal(own.Id, first.Id, second.Id);
    }

    [Fact(DisplayName =
        "Adopting a trailer the show already holds adds nothing, so adopting twice (a retried merge) does not duplicate a trailer id.")]
    public void adopting_twice_does_not_duplicate_a_trailer()
    {
        // Arrange
        var shared = CreateTrailer();
        var show = new TvShow(_fixture.CreateTitle());
        var source = new TvShow(_fixture.CreateTitle()) { Trailers = [shared] };
        show.AdoptTrailersFrom(source);

        // Act
        var adoptedAgain = show.AdoptTrailersFrom(source);

        // Assert
        adoptedAgain.Should().BeEmpty();
        show.Trailers.Should().ContainSingle().Which.Id.Should().Be(shared.Id);
    }

    [Fact(DisplayName =
        "Adopting from a source with no trailers leaves a show without trailers unchanged (still omitted from its JSON).")]
    public void adopting_from_a_source_without_trailers_changes_nothing()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle());
        var source = new TvShow(_fixture.CreateTitle());

        // Act
        var adopted = show.AdoptTrailersFrom(source);

        // Assert
        adopted.Should().BeEmpty();
        show.Trailers.Should().BeNull();
    }

    [Fact(DisplayName =
        "A TV show update re-reads and re-applies the change after an ETag miss, so a trailer attached between the attempts is on the show that gets saved (RPP #1412).")]
    public async Task update_after_an_etag_miss_applies_to_the_newer_show_with_its_trailer()
    {
        // Arrange
        var id = _fixture.CreateGuid();
        var newName = _fixture.CreateTitle();
        var trailer = CreateTrailer();
        var reads = 0;
        TvShow? replaced = null;

        // Act
        var result = await TvShowDocumentUpdate.Apply(
            id,
            show =>
            {
                show.Name = newName;
                return true;
            },
            _ =>
            {
                reads++;
                var show = new TvShow(_fixture.CreateTitle()) { Id = id };
                if (reads > 1)
                {
                    show.Trailers = [trailer];
                }

                return Task.FromResult<TvShowVersion?>(new TvShowVersion(show, $"etag-{reads}"));
            },
            (candidate, etag) =>
            {
                if (etag == "etag-1")
                {
                    return Task.FromResult(false);
                }

                replaced = candidate;
                return Task.FromResult(true);
            });

        // Assert
        result.Should().BeSameAs(replaced);
        replaced!.Name.Should().Be(newName);
        replaced.Trailers.Should().ContainSingle().Which.Id.Should().Be(trailer.Id);
    }

    private TvShowTrailer CreateTrailer() => new()
    {
        Id = _fixture.CreateGuid(),
        Title = _fixture.CreateTitle(),
        Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
        Length = _fixture.CreateDuration()
    };

    private static IReadOnlyList<string> ShapeOf(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => string.Join(
                "|",
                property.Name,
                property.PropertyType.FullName,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                property.GetCustomAttribute<JsonPropertyOrderAttribute>()?.Order,
                property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition,
                property.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() is not null))
            .OrderBy(shape => shape, StringComparer.Ordinal)
            .ToList();
}
