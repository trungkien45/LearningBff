using Volo.Abp.DependencyInjection;

namespace LearningBff.Services.Chat;

public interface IChatAttachmentStorageService
{
    Task<string> SaveAttachmentAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default);
    Task<Stream?> GetAttachmentStreamAsync(string storageFileName, CancellationToken cancellationToken = default);
    Task DeleteAttachmentAsync(string storageFileName, CancellationToken cancellationToken = default);
}

public class ChatAttachmentStorageService : IChatAttachmentStorageService, ITransientDependency
{
    private readonly string _storageDirectory;
    private readonly ILogger<ChatAttachmentStorageService> _logger;

    public ChatAttachmentStorageService(
        IConfiguration configuration,
        ILogger<ChatAttachmentStorageService> logger)
    {
        _logger = logger;

        var configuredPath = configuration["ChatAttachments:StoragePath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            _storageDirectory = Path.Combine(AppContext.BaseDirectory, "App_Data", "ChatAttachments");
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

    public async Task<string> SaveAttachmentAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName)?.ToLowerInvariant() ?? string.Empty;
        var storageFileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = GetSafeFilePath(storageFileName);

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

        return storageFileName;
    }

    public Task<Stream?> GetAttachmentStreamAsync(string storageFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = GetSafeFilePath(storageFileName);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult<Stream?>(fileStream);
    }

    public Task DeleteAttachmentAsync(string storageFileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetSafeFilePath(storageFileName);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete chat attachment: {StorageFileName}", storageFileName);
        }

        return Task.CompletedTask;
    }

    private string GetSafeFilePath(string storageFileName)
    {
        var safeFileName = Path.GetFileName(storageFileName);
        var fullPath = Path.GetFullPath(Path.Combine(_storageDirectory, safeFileName));
        var baseDirFullPath = Path.GetFullPath(_storageDirectory);

        if (!fullPath.StartsWith(baseDirFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid attachment path detected.");
        }

        return fullPath;
    }
}
