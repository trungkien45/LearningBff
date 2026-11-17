using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;

namespace LearningBff.Entities;

public class Message : FullAuditedAggregateRoot<long>
{
    public long ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public Guid SenderId { get; set; }
    public IdentityUser? User { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; }
    public long? ReplyToMessageId { get; set; }
    public ICollection<MessageAttachment> Attachments { get; set; } = [];
}

