using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface ICatalogueParentLookup
{
    /// <summary>The news organisation or TV show with this id (a curator targeting a parent by id).</summary>
    Task<CatalogueParentMatch?> FindById(Guid id);

    /// <summary>Case-insensitive name match across news organisations and TV shows. Many → <see cref="AmbiguousParentNameException"/>.</summary>
    Task<CatalogueParentMatch?> FindByName(string name);

    /// <summary>
    /// The parent whose <c>indexing</c> holds the URL's channel / show id. Many → after the podcast tie-breaks
    /// (no playlist, then the single auto-indexed one) → <see cref="AmbiguousParentNameException"/>.
    /// </summary>
    Task<CatalogueParentMatch?> FindByOwner(EpisodeUrlOwnerIds ids);
}

/// <summary>
/// Finds the <see cref="CatalogueParentMatch"/> a URL should go to instead of a podcast. Ids come from
/// <see cref="CatalogueIndexingSource"/> (RPP #1050), set by kind transfer or the curator PATCH.
/// </summary>
public class CatalogueParentLookup(
    INewsOrganisationRepository newsOrganisations,
    ITvShowRepository tvShows,
    ILogger<CatalogueParentLookup> logger) : ICatalogueParentLookup
{
    private const string MixedKind = "CatalogueParent";

    public async Task<CatalogueParentMatch?> FindById(Guid id)
    {
        var organisation = await newsOrganisations.GetNewsOrganisation(id);
        if (organisation != null)
        {
            return CatalogueParentMatch.For(organisation);
        }

        var show = await tvShows.GetTvShow(id);
        return show == null ? null : CatalogueParentMatch.For(show);
    }

    public async Task<CatalogueParentMatch?> FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var organisations = await PublisherNameAttachLookup.FindByName(newsOrganisations, name);
        var shows = await PublisherNameAttachLookup.FindByName(tvShows, name);
        var matches = organisations.Select(CatalogueParentMatch.For).Concat(shows.Select(CatalogueParentMatch.For))
            .ToList();
        return Single(matches, name.Trim());
    }

    public async Task<CatalogueParentMatch?> FindByOwner(EpisodeUrlOwnerIds ids)
    {
        if (ids.IsEmpty)
        {
            return null;
        }

        var matches = new List<CatalogueParentMatch>();
        if (!string.IsNullOrWhiteSpace(ids.YouTubeChannelId))
        {
            var channelId = ids.YouTubeChannelId;
            matches.AddRange((await newsOrganisations
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            matches.AddRange((await tvShows
                    .GetAllBy(x => x.Indexing != null && x.Indexing.YouTubeChannelId == channelId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            if (matches.Count > 1)
            {
                // Same tie-breaks as PodcastService for podcasts sharing a channel.
                var channelWide = matches.Where(x => string.IsNullOrWhiteSpace(x.Indexing?.YouTubePlaylistId)).ToList();
                if (channelWide.Count > 0)
                {
                    matches = channelWide;
                }

                if (matches.Count > 1 && matches.Count(x => x.Indexing?.IsAutoIndexed == true) == 1)
                {
                    matches = matches.Where(x => x.Indexing?.IsAutoIndexed == true).ToList();
                }
            }

            return Single(matches, channelId);
        }

        if (!string.IsNullOrWhiteSpace(ids.SpotifyShowId))
        {
            var spotifyId = ids.SpotifyShowId;
            matches.AddRange((await newsOrganisations
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            matches.AddRange((await tvShows
                    .GetAllBy(x => x.Indexing != null && x.Indexing.SpotifyId == spotifyId).ToListAsync())
                .Select(CatalogueParentMatch.For));
            return Single(matches, spotifyId);
        }

        var appleId = ids.ApplePodcastId!.Value;
        matches.AddRange((await newsOrganisations
                .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == appleId).ToListAsync())
            .Select(CatalogueParentMatch.For));
        matches.AddRange((await tvShows
                .GetAllBy(x => x.Indexing != null && x.Indexing.AppleId == appleId).ToListAsync())
            .Select(CatalogueParentMatch.For));
        return Single(matches, appleId.ToString());
    }

    private CatalogueParentMatch? Single(IReadOnlyList<CatalogueParentMatch> matches, string key)
    {
        if (matches.Count <= 1)
        {
            return matches.SingleOrDefault();
        }

        var kinds = matches.Select(x => x.ContentKind).Distinct().ToArray();
        var ids = matches.Select(x => x.Parent.Id).ToArray();
        logger.LogWarning(
            "'{Key}' matches {Count} news organisations / TV shows; refusing to pick one. Ids: {Ids}.",
            key, matches.Count, string.Join(", ", ids));
        throw new AmbiguousParentNameException(kinds.Length == 1 ? kinds[0] : MixedKind, key, ids);
    }
}
