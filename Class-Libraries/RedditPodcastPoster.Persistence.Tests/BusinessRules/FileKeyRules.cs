using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

/// <summary>
/// Stored file keys name files in backups and the public JSON database, which refuses an empty key. Names written
/// only in a non-Latin script (or only emoji) used to give an empty key.
/// </summary>
public class FileKeyRules
{
    // The rules are about names with no Latin letters or digits, so these exact strings matter.
    private const string KoreanName = "오늘의 뉴스";
    private const string OtherKoreanName = "주간 이야기";
    private const string EmojiOnlyName = "🎙️📻 ✨";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Podcast> _podcasts = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<Film> _films = [];

    public FileKeyRules()
    {
        Filterable<IPodcastRepository, Podcast>(_podcasts);
        Filterable<ITvShowRepository, TvShow>(_shows);
        Filterable<INewsOrganisationRepository, NewsOrganisation>(_organisations);
        Filterable<IFilmRepository, Film>(_films);
    }

    private IFileKeyAllocator Allocator => _mocker.CreateInstance<FileKeyAllocator>();

    [Theory(DisplayName =
        "A name with no Latin letters or digits (Korean only, emoji only) gives a stored key built from the id, never an empty key, because the public database refuses empty keys.")]
    [InlineData(KoreanName)]
    [InlineData(EmojiOnlyName)]
    public void non_latin_name_gets_id_slug(string name)
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var slug = FileKeyFactory.GetFileKey(name, id);

        // Assert
        FileKeyFactory.IsUsableSlug(FileKeyFactory.GetFileKey(name)).Should()
            .BeFalse("the plain slug strips every non-Latin character, leaving at most underscores");
        slug.Should().Be(FileKeyFactory.IdSlugPrefix + id.ToString("N")[..FileKeyFactory.IdSlugLength]);
        FileKeyFactory.IsUsableSlug(slug).Should().BeTrue();
    }

    [Fact(DisplayName =
        "The id fallback is deterministic: the same id always gives the same key, so a retry does not mint a different file.")]
    public void id_slug_is_stable()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var first = FileKeyFactory.GetNewsOrganisationFileKey(KoreanName, id);
        var second = FileKeyFactory.GetNewsOrganisationFileKey(OtherKoreanName, id);

        // Assert
        first.Should().Be(second);
        first.Should().StartWith(FileKeyFactory.NewsOrganisationPrefix + FileKeyFactory.IdSlugPrefix);
    }

    [Fact(DisplayName =
        "A Latin name keeps exactly the slug it always had, so existing keys and files do not move.")]
    public void latin_name_keeps_existing_slug()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var id = _fixture.CreateGuid();

        // Act
        var slug = FileKeyFactory.GetFileKey(name, id);

        // Assert
        slug.Should().Be(FileKeyFactory.GetFileKey(name));
        FileKeyFactory.GetTvShowFileKey(name, id).Should().Be(FileKeyFactory.GetTvShowFileKey(name));
        FileKeyFactory.GetFilmFileKey(name, id).Should().Be(FileKeyFactory.GetFilmFileKey(name));
    }

    [Theory(DisplayName =
        "Empty, underscore-only and one-character slugs are placeholders, not usable keys; two or more Latin letters or digits are usable.")]
    [InlineData("", false)]
    [InlineData("_", false)]
    [InlineData("__2", false)]
    [InlineData("_2", false)]
    [InlineData("x", false)]
    [InlineData("tv", true)]
    [InlineData("24", true)]
    [InlineData("a_b", true)]
    public void placeholder_slugs_are_not_usable(string slug, bool usable)
    {
        // Arrange
        var prefixed = FileKeyFactory.FilmPrefix + slug;

        // Act
        var slugUsable = FileKeyFactory.IsUsableSlug(slug);
        var keyUsable = FileKeyFactory.IsUsableFileKey(prefixed, FileKeyFactory.FilmPrefix);

        // Assert
        slugUsable.Should().Be(usable);
        keyUsable.Should().Be(usable);
    }

    [Fact(DisplayName =
        "A name with no usable slug uses the fallback slug when it is usable, so a podcast that moves kind keeps its own key under the new prefix.")]
    public void fallback_slug_beats_id_slug()
    {
        // Arrange
        var fallback = FileKeyFactory.GetFileKey(_fixture.CreateTitle());

        // Act
        var key = FileKeyFactory.GetTvShowFileKey(KoreanName, _fixture.CreateGuid(), fallback);

        // Assert
        key.Should().Be(FileKeyFactory.TvShowPrefix + fallback);
    }

    [Fact(DisplayName =
        "Mapping a Korean-named podcast to a news organisation keeps the podcast's own usable key under the news prefix, instead of the empty 'news-' key it used to get.")]
    public void mapper_uses_podcast_key_for_non_latin_name()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = KoreanName;
            p.FileKey = FileKeyFactory.GetFileKey(_fixture.CreateTitle());
        });

        // Act
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var show = CatalogueParentKindMapper.ToTvShow(podcast);

        // Assert
        organisation.FileKey.Should().Be(FileKeyFactory.NewsOrganisationPrefix + podcast.FileKey);
        show.FileKey.Should().Be(FileKeyFactory.TvShowPrefix + podcast.FileKey);
    }

    [Fact(DisplayName =
        "Mapping a Korean-titled episode to a film gives a film key built from the episode id, never the bare 'film-' prefix.")]
    public void mapper_film_uses_episode_id_for_non_latin_title()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Title = KoreanName);

        // Act
        var film = CatalogueParentKindMapper.ToFilm(podcast, episode);

        // Assert
        film.FileKey.Should().Be(FileKeyFactory.FilmPrefix + FileKeyFactory.GetIdSlug(episode.Id));
    }

    [Fact(DisplayName =
        "A new item whose key is already used by another item of the same kind (ignoring case) gets the next free _2, _3 suffix, so two files never share a name.")]
    public async Task allocate_suffixes_duplicates()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var key = FileKeyFactory.GetTvShowFileKey(name);
        _shows.Add(new TvShow(name) { Id = _fixture.CreateGuid(), FileKey = key.ToUpperInvariant() });
        _shows.Add(new TvShow(name) { Id = _fixture.CreateGuid(), FileKey = key + "_2" });

        // Act
        var allocated = await Allocator.Allocate(FileKeyKind.TvShow, name, _fixture.CreateGuid());

        // Assert
        allocated.Should().Be(key + "_3");
    }

    [Fact(DisplayName =
        "Uniqueness is per kind: a podcast key does not block the same slug for a news organisation, because the news key carries its own prefix.")]
    public async Task allocate_is_scoped_to_kind()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _podcasts.Add(_fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(name)));

        // Act
        var allocated = await Allocator.Allocate(FileKeyKind.NewsOrganisation, name, _fixture.CreateGuid());

        // Assert
        allocated.Should().Be(FileKeyFactory.GetNewsOrganisationFileKey(name));
    }

    [Fact(DisplayName =
        "Two Korean-named podcasts get two different usable keys, because each falls back to its own id.")]
    public async Task two_non_latin_creates_get_distinct_keys()
    {
        // Arrange
        var allocator = Allocator;

        // Act
        var first = await allocator.Allocate(FileKeyKind.Podcast, KoreanName, _fixture.CreateGuid());
        var second = await allocator.Allocate(FileKeyKind.Podcast, KoreanName, _fixture.CreateGuid());

        // Assert
        first.Should().NotBe(second);
        FileKeyFactory.IsUsableSlug(first).Should().BeTrue();
        FileKeyFactory.IsUsableSlug(second).Should().BeTrue();
    }

    [Fact(DisplayName =
        "An item's own stored key is not a collision for itself, so re-allocating on a retry gives the same key, not a _2.")]
    public async Task own_key_is_not_a_collision()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var film = new Film(name) { Id = _fixture.CreateGuid() };
        film.FileKey = FileKeyFactory.GetFilmFileKey(name);
        _films.Add(film);

        // Act
        var allocated = await Allocator.Allocate(FileKeyKind.Film, name, film.Id);

        // Assert
        allocated.Should().Be(film.FileKey);
    }

    [Fact(DisplayName =
        "Keys marked as taken but not stored yet (handed out earlier in the same process) are skipped too.")]
    public async Task also_taken_keys_are_skipped()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var key = FileKeyFactory.GetFileKey(name);

        // Act
        var allocated = await Allocator.Allocate(
            FileKeyKind.Podcast, name, _fixture.CreateGuid(), alsoTaken: k => k == key);

        // Assert
        allocated.Should().Be(key + "_2");
    }

    [Fact(DisplayName =
        "Renaming a podcast to a Korean-only name keeps its current usable key rather than losing it to an empty or id key.")]
    public async Task rename_to_non_latin_keeps_usable_current_key()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(_fixture.CreateTitle()));
        _podcasts.Add(podcast);

        // Act
        var key = await Allocator.AllocateForRename(FileKeyKind.Podcast, KoreanName, podcast.Id, podcast.FileKey);

        // Assert
        key.Should().Be(podcast.FileKey);
    }

    [Fact(DisplayName =
        "Renaming a podcast with an empty key to a Korean-only name gives it an id key, so the public database can publish it.")]
    public async Task rename_to_non_latin_repairs_empty_key()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.FileKey = string.Empty);
        _podcasts.Add(podcast);

        // Act
        var key = await Allocator.AllocateForRename(FileKeyKind.Podcast, KoreanName, podcast.Id, podcast.FileKey);

        // Assert
        key.Should().Be(FileKeyFactory.GetIdSlug(podcast.Id));
    }

    [Fact(DisplayName =
        "A rename that re-keys to a Latin name another podcast already uses gets the _2 suffix instead of sharing that podcast's file.")]
    public async Task rename_to_taken_latin_name_is_suffixed()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _podcasts.Add(_fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(name)));
        var renamed = _fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(_fixture.CreateTitle()));
        _podcasts.Add(renamed);

        // Act
        var key = await Allocator.AllocateForRename(FileKeyKind.Podcast, name, renamed.Id, renamed.FileKey);

        // Assert
        key.Should().Be(FileKeyFactory.GetFileKey(name) + "_2");
    }

    [Fact(DisplayName =
        "A rename whose new key equals the current key (ignoring case) keeps the current key unchanged.")]
    public async Task rename_to_same_key_keeps_it()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var podcast = _fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(name).ToUpperInvariant());
        _podcasts.Add(podcast);

        // Act
        var key = await Allocator.AllocateForRename(FileKeyKind.Podcast, name, podcast.Id, podcast.FileKey);

        // Assert
        key.Should().Be(podcast.FileKey);
    }

    [Fact(DisplayName =
        "A rename that keeps keys never changes a usable key, but repairs an empty or placeholder one from the new name.")]
    public async Task repair_only_replaces_unusable_keys()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var usable = FileKeyFactory.GetNewsOrganisationFileKey(_fixture.CreateTitle());
        var id = _fixture.CreateGuid();

        // Act
        var kept = await Allocator.RepairIfUnusable(FileKeyKind.NewsOrganisation, name, id, usable);
        var repairedEmpty = await Allocator.RepairIfUnusable(FileKeyKind.NewsOrganisation, name, id, string.Empty);
        var repairedPlaceholder = await Allocator.RepairIfUnusable(
            FileKeyKind.NewsOrganisation, name, id, FileKeyFactory.NewsOrganisationPrefix + "_2");

        // Assert
        kept.Should().Be(usable);
        repairedEmpty.Should().Be(FileKeyFactory.GetNewsOrganisationFileKey(name));
        repairedPlaceholder.Should().Be(FileKeyFactory.GetNewsOrganisationFileKey(name));
    }

    [Fact(DisplayName =
        "Creating a TV show, news organisation or film from a Korean-only name never leaves an empty slug after the kind prefix.")]
    public void constructors_never_leave_empty_slug()
    {
        // Arrange
        var name = KoreanName;

        // Act
        var keys = new[]
        {
            (new TvShow(name).FileKey, FileKeyFactory.TvShowPrefix),
            (new NewsOrganisation(name).FileKey, FileKeyFactory.NewsOrganisationPrefix),
            (new Film(name).FileKey, FileKeyFactory.FilmPrefix)
        };

        // Assert
        keys.Should().AllSatisfy(k => FileKeyFactory.IsUsableFileKey(k.FileKey, k.Item2).Should().BeTrue());
    }

    private void Filterable<TRepository, TEntity>(List<TEntity> store)
        where TRepository : class, IFilterableRepository<TEntity>
        where TEntity : CosmosSelector =>
        _mocker.GetMock<TRepository>()
            .Setup(r => r.GetAllBy(
                It.IsAny<Expression<Func<TEntity, bool>>>(),
                It.IsAny<Expression<Func<TEntity, string>>>()))
            .Returns((Expression<Func<TEntity, bool>> selector, Expression<Func<TEntity, string>> projection) =>
                store.Where(selector.Compile()).Select(projection.Compile()).ToAsyncEnumerable());
}
