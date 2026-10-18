using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace LearningBff.Services.LessonImages;

[ExposeServices(typeof(ILessonImageStorageService), typeof(LessonImageFileStorageService))]
public class LessonImageFileStorageService : ILessonImageStorageService, ITransientDependency
{
    private readonly string _storageDirectory;
    private readonly ILogger<LessonImageFileStorageService> _logger;

    public LessonImageFileStorageService(
        IConfiguration configuration,
        ILogger<LessonImageFileStorageService> logger)
    {
        _logger = logger;

        var configuredPath = configuration["LessonImages:StoragePath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "LessonImages");
        }
        else
        {
            _storageDirectory = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppContext.BaseDirectory, configuredPath);
        }

        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }

    public async Task<string> SaveBlobAsync(Stream stream, string extension, CancellationToken cancellationToken = default)
    {
        var cleanExt = string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.Trim().ToLowerInvariant();
        if (!cleanExt.StartsWith('.'))
        {
            cleanExt = "." + cleanExt;
        }

        var blobName = $"{Guid.NewGuid():N}{cleanExt}";
        var fullPath = GetSafeFilePath(blobName);

        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var fileStream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            await stream.CopyToAsync(fileStream, cancellationToken);
        }

        return blobName;
    }

    public Task<Stream?> GetBlobStreamAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFilePath(blobName);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task DeleteBlobAsync(string blobName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFilePath(blobName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete lesson image blob: {BlobName}", blobName);
        }

        return Task.CompletedTask;
    }

    private string GetSafeFilePath(string blobName)
    {
        var safeFileName = Path.GetFileName(blobName);
        var fullPath = Path.GetFullPath(Path.Combine(_storageDirectory, safeFileName));
        var baseDirFullPath = Path.GetFullPath(_storageDirectory);

        if (!fullPath.StartsWith(baseDirFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid blob path detected.");
        }

        return fullPath;
    }
}
