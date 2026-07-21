using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Persistence;

public class SafeFileEntityWriter(
    IFileRepository fileRepository
) : ISafeFileEntityWriter
{
    public async Task Write<T>(T data) where T : CosmosSelector
    {
        if (string.IsNullOrWhiteSpace(data.FileKey))
        {
            throw new InvalidOperationException($"Entity with id '{data.Id}' has empty file-key.");
        }

        var filePath = fileRepository.GetFilePath(data);
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException(
                $"File with path '{filePath}' already exists when writing item with id '{data.Id}'.");
        }

        await fileRepository.Write(data);
    }
}