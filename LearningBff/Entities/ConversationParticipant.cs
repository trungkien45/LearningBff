using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;

namespace LearningBff.Entities;

public class ConversationParticipant : FullAuditedAggregateRoot<long>
{
    public long ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public Guid UserId { get; set; }
    public IdentityUser? User { get; set; }
    public Role Role { get; set; }
    public DateTime? JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public bool IsMuted { get; set; }
    public long? LastReadMessageId { get; set; }
}

