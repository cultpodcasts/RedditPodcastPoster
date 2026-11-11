using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.YouTube;

public class ChannelWideYouTubeClaimGuard(
    IPodcastRepository podcastRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    ITvShowRepository tvShowRepository,
    ILogger<ChannelWideYouTubeClaimGuard> logger) : IChannelWideYouTubeClaimGuard
{
    public async Task<ChannelWideYouTubeClaimConflict?> Check(
        Guid entityId,
        ChannelWideYouTubeClaimKind kind,
        string? projectedChannelId,
        string? projectedPlaylistId,
        bool projectedRemoved,
        CancellationToken cancellationToken = default)
    {
        if (projectedRemoved)
        {
            return null;
        }

        var channelId = projectedChannelId?.Trim();
        if (string.IsNullOrEmpty(channelId) || !string.IsNullOrWhiteSpace(projectedPlaylistId))
        {
            return null;
        }

        var conflicting = new List<Guid>();

        if (kind != ChannelWideYouTubeClaimKind.Podcast)
        {
            await foreach (var podcast in podcastRepository.GetAllBy(p => p.YouTubeChannelId == channelId)
                               .WithCancellation(cancellationToken))
            {
                if (podcast.IsRemoved() || !string.IsNullOrWhiteSpace(podcast.YouTubePlaylistId))
                {
                    continue;
                }

                conflicting.Add(podcast.Id);
            }
        }

        if (kind != ChannelWideYouTubeClaimKind.NewsOrganisation)
        {
            Expression<Func<NewsOrganisation, bool>> match = x =>
                x.Id != entityId && x.Indexing != null &&
                (x.Indexing.YouTubeChannelId == channelId ||
                 (x.Indexing.AdditionalYouTubeChannelIds != null &&
                  x.Indexing.AdditionalYouTubeChannelIds.Contains(channelId)));
            await foreach (var organisation in newsOrganisationRepository.GetAllBy(match)
                               .WithCancellation(cancellationToken))
            {
                // An additional channel (#1370) is always claimed channel-wide, whatever the main playlist.
                if (organisation.IsRemoved() || organisation.Indexing?.ClaimsYouTubeChannelWide(channelId) != true)
                {
                    continue;
                }

                conflicting.Add(organisation.Id);
            }
        }

        if (kind != ChannelWideYouTubeClaimKind.TvShow)
        {
            Expression<Func<TvShow, bool>> match = x =>
                x.Id != entityId && x.Indexing != null &&
                (x.Indexing.YouTubeChannelId == channelId ||
                 (x.Indexing.AdditionalYouTubeChannelIds != null &&
                  x.Indexing.AdditionalYouTubeChannelIds.Contains(channelId)));
            await foreach (var show in tvShowRepository.GetAllBy(match).WithCancellation(cancellationToken))
            {
                if (show.IsRemoved() || show.Indexing?.ClaimsYouTubeChannelWide(channelId) != true)
                {
                    continue;
                }

                conflicting.Add(show.Id);
            }
        }

        if (conflicting.Count == 0)
        {
            return null;
        }

        var conflict = new ChannelWideYouTubeClaimConflict(channelId, conflicting);
        logger.LogWarning(
            "Channel-wide YouTube claim refused for {Kind} '{EntityId}' on channel '{ChannelId}': conflicts with {ConflictingIds}.",
            kind, entityId, channelId, string.Join(", ", conflicting));
        return conflict;
    }
}
