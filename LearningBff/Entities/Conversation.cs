using Volo.Abp.Domain.Entities.Auditing;

namespace LearningBff.Entities;

public class Conversation : FullAuditedAggregateRoot<long>
{
    public long? SubjectId { get; set; }
    public Subject? Subject { get; set; }
    public string Title { get; set; } = string.Empty;
    public TypeConversation Type { get; set; } = TypeConversation.Group;
    public string? AvatarUrl { get; set; }
    public ICollection<ConversationParticipant> Participants { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}

