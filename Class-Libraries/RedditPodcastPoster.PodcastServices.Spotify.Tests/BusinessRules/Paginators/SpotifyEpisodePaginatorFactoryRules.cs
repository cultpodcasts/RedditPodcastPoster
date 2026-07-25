using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Paginators;

/// <summary>
/// The factory is the single seam through which SpotifyQueryPaginator selects a date-window walk
/// strategy, so each method must yield the paginator implementing that strategy.
/// </summary>
public class SpotifyEpisodePaginatorFactoryRules
{
    private readonly SpotifyEpisodePaginatorFactory _sut =
        new(
            NullLogger<SimpleEpisodePaginator>.Instance,
            NullLogger<AscendingEpisodePaginator>.Instance);

    [Fact(DisplayName =
        "CreateReverseChronologicalPaginator returns a SimpleEpisodePaginator " +
        "because newest-first catalogues stop paging via the ReleasedSince early-stop.")]
    public void Reverse_chronological_creates_simple_episode_paginator()
    {
        var paginator = _sut.CreateReverseChronologicalPaginator(DateTime.UtcNow.Date.AddDays(-3));

        paginator.Should().BeOfType<SimpleEpisodePaginator>();
    }

    [Fact(DisplayName =
        "CreateAscendingEndJumpPaginator returns an AscendingEpisodePaginator " +
        "because oldest-first catalogues must jump to the final page and walk backwards.")]
    public void Ascending_end_jump_creates_ascending_episode_paginator()
    {
        var paginator = _sut.CreateAscendingEndJumpPaginator(DateTime.UtcNow.Date.AddDays(-3));

        paginator.Should().BeOfType<AscendingEpisodePaginator>();
    }
}
