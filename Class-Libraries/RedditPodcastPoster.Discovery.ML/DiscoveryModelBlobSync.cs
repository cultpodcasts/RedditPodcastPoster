using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Discovery.ML;

public static class DiscoveryModelBlobSync
{
    public static string? ResolveModelDirectory(DiscoveryScorerSettings settings, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(settings.ModelDirectory))
        {
            return Path.GetFullPath(settings.ModelDirectory);
        }

        if (string.IsNullOrWhiteSpace(settings.BlobContainerName) ||
            string.IsNullOrWhiteSpace(settings.BlobStorageAccountName))
        {
            return null;
        }

        try
        {
            return DownloadModelBundle(settings, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync discovery scorer model from blob storage.");
            return null;
        }
    }

    private static string DownloadModelBundle(DiscoveryScorerSettings settings, ILogger logger)
    {
        var cacheDirectory = Path.Combine(
            Path.GetTempPath(),
            "discovery-scorer-model",
            settings.BlobPrefix?.Trim().Trim('/') ?? "current");
        Directory.CreateDirectory(cacheDirectory);

        var credential = CreateCredential(settings);
        var serviceUri = new Uri($"https://{settings.BlobStorageAccountName}.blob.core.windows.net");
        var containerClient = new BlobServiceClient(serviceUri, credential)
            .GetBlobContainerClient(settings.BlobContainerName);
        var prefix = NormalizePrefix(settings.BlobPrefix);

        var manifestBlobName = $"{prefix}{settings.ManifestFileName}";
        var remoteManifest = containerClient.GetBlobClient(manifestBlobName);
        if (!remoteManifest.Exists())
        {
            throw new FileNotFoundException(
                $"Discovery scorer manifest blob '{manifestBlobName}' was not found in container '{settings.BlobContainerName}'.");
        }

        var localManifestPath = Path.Combine(cacheDirectory, settings.ManifestFileName);
        var remoteProperties = remoteManifest.GetProperties().Value;
        var shouldDownloadAll = ShouldDownloadAll(localManifestPath, remoteProperties.ETag.ToString(), remoteProperties.LastModified);

        var filesToSync = new[]
        {
            settings.ModelFileName,
            settings.ManifestFileName,
            settings.OnnxModelFileName,
            settings.TokenizerFileName,
            settings.ShowAcceptRatesFileName
        };

        foreach (var fileName in filesToSync)
        {
            var blobName = $"{prefix}{fileName}";
            var localPath = Path.Combine(cacheDirectory, fileName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!blobClient.Exists())
            {
                if (fileName == settings.ShowAcceptRatesFileName)
                {
                    logger.LogWarning("Optional scorer blob '{BlobName}' not found; continuing.", blobName);
                    continue;
                }

                throw new FileNotFoundException(
                    $"Discovery scorer blob '{blobName}' was not found in container '{settings.BlobContainerName}'.");
            }

            if (shouldDownloadAll || !File.Exists(localPath))
            {
                logger.LogInformation("Downloading discovery scorer blob '{BlobName}'...", blobName);
                blobClient.DownloadTo(localPath);
            }
        }

        File.WriteAllText(localManifestPath + ".etag", remoteProperties.ETag.ToString());

        if (File.Exists(localManifestPath))
        {
            var manifest = JsonSerializer.Deserialize<DiscoveryAcceptManifest>(File.ReadAllText(localManifestPath));
            if (manifest != null)
            {
                logger.LogInformation(
                    "Discovery scorer model ready from blob cache (trained {TrainedAt:O}, {Rows:N0} rows).",
                    manifest.TrainedAt,
                    manifest.TrainingRows);
            }
        }

        return cacheDirectory;
    }

    private static bool ShouldDownloadAll(string localManifestPath, string remoteEtag, DateTimeOffset remoteLastModified)
    {
        if (!File.Exists(localManifestPath))
        {
            return true;
        }

        var sidecarPath = localManifestPath + ".etag";
        if (!File.Exists(sidecarPath))
        {
            return true;
        }

        var cached = File.ReadAllText(sidecarPath);
        if (!string.Equals(cached, remoteEtag, StringComparison.Ordinal))
        {
            return true;
        }

        return File.GetLastWriteTimeUtc(localManifestPath) < remoteLastModified.UtcDateTime;
    }

    private static TokenCredential CreateCredential(DiscoveryScorerSettings settings)
    {
        var clientId = settings.ManagedIdentityClientId
                       ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage__clientId");

        return string.IsNullOrWhiteSpace(clientId)
            ? new DefaultAzureCredential()
            : new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = clientId
            });
    }

    private static string NormalizePrefix(string? blobPrefix)
    {
        if (string.IsNullOrWhiteSpace(blobPrefix))
        {
            return string.Empty;
        }

        var trimmed = blobPrefix.Trim().Trim('/');
        return $"{trimmed}/";
    }
}
