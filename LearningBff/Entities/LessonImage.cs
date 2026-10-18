using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace LearningBff.Entities;

public class LessonImage : FullAuditedEntity<Guid>
{
    public long? LessonId { get; set; }
    public Lesson? Lesson { get; set; }

    public long? ChapterId { get; set; }
    public Chapter? Chapter { get; set; }

    public string BlobName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }

    protected LessonImage()
    {
    }

    public LessonImage(
        Guid id,
        string blobName,
        string originalFileName,
        string contentType,
        long fileSize,
        long? lessonId = null,
        long? chapterId = null) : base(id)
    {
        BlobName = blobName;
        OriginalFileName = originalFileName;
        ContentType = contentType;
        FileSize = fileSize;
        LessonId = lessonId;
        ChapterId = chapterId;
    }
}
