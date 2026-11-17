using Volo.Abp.Domain.Entities.Auditing;

namespace LearningBff.Entities;

public class MessageAttachment : FullAuditedAggregateRoot<long>
{
    public long MessageId { get; set; }
    public Message? Message { get; set; }
    public string BlobName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

