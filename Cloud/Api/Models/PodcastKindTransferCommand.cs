namespace Api.Models;

public record PodcastKindTransferCommand(Guid PodcastId, PodcastKindTransferRequest Request);
