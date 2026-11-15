using FluentAssertions;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

public class SubmitUrlIdentityHoldNoticeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a console submit is held for an identity choice, the notice says the film was skipped and lists each " +
        "candidate with its year and key, because the console cannot pick and must not skip silently.")]
    public void identity_hold_names_the_skip_and_candidates()
    {
        // Arrange
        var query = _fixture.CreateTitle();
        var candidateTitle = _fixture.CreateTitle();
        var tmdbId = (int)(_fixture.CreateAppleId() % 1_000_000_000L) + 1;
        var year = DomainTestFixture.UtcToday.Year;
        var candidate = new CatalogueAuthorityCandidate(
            CatalogueAuthorityCandidate.TmdbKey(tmdbId),
            candidateTitle,
            year,
            CatalogueAuthorityCandidate.FilmKind,
            CatalogueAuthorityCandidate.TmdbSource,
            tmdbId,
            TvdbId: null);
        var result = new SubmitResult(
            SubmitResultState.None,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Film,
            IdentityHold: new CatalogueIdentityHold(CatalogueAuthorityCandidate.FilmKind, query, [candidate]));

        // Act
        var notice = SubmitUrlIdentityHoldNotice.From(result);

        // Assert
        notice.Should().NotBeNull();
        notice.Should().StartWith("Skipped:");
        notice.Should().Contain(query);
        notice.Should().Contain($"{candidateTitle} ({year})");
        notice.Should().Contain(CatalogueAuthorityCandidate.TmdbKey(tmdbId));
    }

    [Fact(DisplayName = "When a console submit was not held, there is no identity notice.")]
    public void no_hold_has_no_notice()
    {
        // Arrange
        var result = new SubmitResult(
            SubmitResultState.Created,
            SubmitResultState.None,
            ContentKind: SubmitClassification.Film);

        // Act
        var notice = SubmitUrlIdentityHoldNotice.From(result);

        // Assert
        notice.Should().BeNull();
    }
}
