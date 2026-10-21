using System.Linq.Expressions;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Catalogue;

/// <summary>A service id a PATCH would give to more than one submit target.</summary>
public sealed record ServiceIdConflict(string Field, string Value, IReadOnlyList<Guid> OwnerIds)
{
    public string Message =>
        $"{Field} '{Value}' is already used by {string.Join(", ", OwnerIds)}; URL submission could not tell them apart.";
}

/// <summary>
/// Read-only check run before a news organisation / TV show PATCH stores service ids, so URL submission keeps one
/// owner per id. Only ids the PATCH sets are checked (clearing never conflicts).
/// <list type="bullet">
/// <item>Another news organisation or TV show with the same <c>youTubeChannelId</c>, <c>spotifyId</c> or
/// <c>appleId</c>: conflict.</item>
/// <item>A podcast with the same <c>spotifyId</c> / <c>appleId</c>, or a channel-wide podcast (no playlist) on the same
/// <c>youTubeChannelId</c>: conflict, because that podcast would win every submit and the parent's id would be dead.</item>
/// <item>A playlist-scoped podcast on the same channel: allowed with a Warning. A show podcast with its own playlist
/// on an outlet's channel is legitimate (for example "Here's the Scoop" on NBC News); submission routes by playlist
/// membership (#1054).</item>
/// </list>
/// Removed podcasts are ignored, as submission ignores them.
/// </summary>
public interface IServiceIdsUniqueness
{
    Task<ServiceIdConflict?> FindConflict(Guid parentId, ServiceIdsChange change);
}

public class ServiceIdsUniqueness(
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    IPodcastRepository podcastRepository,
    ILogger<ServiceIdsUniqueness> logger) : IServiceIdsUniqueness
{
    public async Task<ServiceIdConflict?> FindConflict(Guid parentId, ServiceIdsChange change)
    {
        var channelId = change.YouTubeChannelId?.Trim();
        if (!string.IsNullOrEmpty(channelId))
        {
            var owners = await ParentOwners(
                x => x.Id != parentId && x.Indexing != null && x.Indexing.YouTubeChannelId == channelId,
                x => x.Id != parentId && x.Indexing != null && x.Indexing.YouTubeChannelId == channelId);
            var podcasts = await Podcasts(x => x.YouTubeChannelId == channelId);
            owners.AddRange(podcasts.Where(p => string.IsNullOrEmpty(p.YouTubePlaylistId)).Select(p => p.Id));
            if (owners.Count > 0)
            {
                return new ServiceIdConflict("youTubeChannelId", channelId, owners);
            }

            foreach (var podcast in podcasts)
            {
                logger.LogWarning(
                    "Parent '{ParentId}' shares youTubeChannelId '{ChannelId}' with playlist-scoped podcast '{PodcastId}' ('{PodcastName}', playlist '{PlaylistId}'); submission routes by playlist membership.",
                    parentId, channelId, podcast.Id, podcast.Name, podcast.YouTubePlaylistId);
            }
        }

        var spotifyId = change.SpotifyId?.Trim();
        if (!string.IsNullOrEmpty(spotifyId))
        {
            var owners = await ParentOwners(
                x => x.Id != parentId && x.Indexing != null && x.Indexing.SpotifyId == spotifyId,
                x => x.Id != parentId && x.Indexing != null && x.Indexing.SpotifyId == spotifyId);
            owners.AddRange((await Podcasts(x => x.SpotifyId == spotifyId)).Select(p => p.Id));
            if (owners.Count > 0)
            {
                return new ServiceIdConflict("spotifyId", spotifyId, owners);
            }
        }

        if (change.NullAppleId != true && change.AppleId is { } appleId)
        {
            var owners = await ParentOwners(
                x => x.Id != parentId && x.Indexing != null && x.Indexing.AppleId == appleId,
                x => x.Id != parentId && x.Indexing != null && x.Indexing.AppleId == appleId);
            owners.AddRange((await Podcasts(x => x.AppleId == appleId)).Select(p => p.Id));
            if (owners.Count > 0)
            {
                return new ServiceIdConflict("appleId", appleId.ToString(), owners);
            }
        }

        return null;
    }

    private async Task<List<Guid>> ParentOwners(
        Expression<Func<NewsOrganisation, bool>> organisationMatch,
        Expression<Func<TvShow, bool>> showMatch)
    {
        var ids = new List<Guid>();
        await foreach (var id in newsOrganisationRepository.GetAllBy(organisationMatch, x => x.Id))
        {
            ids.Add(id);
        }

        await foreach (var id in tvShowRepository.GetAllBy(showMatch, x => x.Id))
        {
            ids.Add(id);
        }

        return ids;
    }

    private async Task<List<Podcast>> Podcasts(Expression<Func<Podcast, bool>> match)
    {
        var podcasts = new List<Podcast>();
        await foreach (var podcast in podcastRepository.GetAllBy(match))
        {
            if (!podcast.IsRemoved())
            {
                podcasts.Add(podcast);
            }
        }

        return podcasts;
    }
}
