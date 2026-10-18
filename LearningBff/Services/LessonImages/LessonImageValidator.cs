using System;
using System.IO;
using Volo.Abp;

namespace LearningBff.Services.LessonImages;

public static class LessonImageValidator
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif"
    };

    public static void Validate(Stream stream, string fileName, string? contentType, long fileSize)
    {
        if (fileSize <= 0 || stream.Length == 0)
        {
            throw new UserFriendlyException("File ảnh tải lên không được rỗng.");
        }

        if (fileSize > MaxFileSizeBytes || stream.Length > MaxFileSizeBytes)
        {
            throw new UserFriendlyException("Dung lượng ảnh vượt quá giới hạn tối đa 5 MB.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new UserFriendlyException("Định dạng file không được hỗ trợ. Chỉ chấp nhận các định dạng: JPG, JPEG, PNG, WEBP, GIF.");
        }

        // Validate magic bytes
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var header = new byte[12];
        var bytesRead = stream.Read(header, 0, header.Length);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        if (bytesRead < 4)
        {
            throw new UserFriendlyException("Nội dung file không hợp lệ.");
        }

        var isJpeg = header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47;
        var isGif = header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38;
        var isWebp = bytesRead >= 12
            && header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 // RIFF
            && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50; // WEBP

        if (!isJpeg && !isPng && !isGif && !isWebp)
        {
            throw new UserFriendlyException("Nội dung file không phải là ảnh hợp lệ (chỉ chấp nhận JPEG, PNG, GIF, WebP).");
        }
    }

    public static string NormalizeContentType(string extension, string? providedContentType)
    {
        var ext = extension.Trim().ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => string.IsNullOrWhiteSpace(providedContentType) ? "application/octet-stream" : providedContentType
        };
    }
}
