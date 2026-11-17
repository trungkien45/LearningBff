using Volo.Abp.Application.Dtos;
using LearningBff.Entities;

namespace LearningBff.Dtos.Chat;

public class ConversationDto : EntityDto<long>
{
    public long? SubjectId { get; set; }
    public string? SubjectName { get; set; }
    public string Title { get; set; } = string.Empty;
    public TypeConversation Type { get; set; }
    public string? AvatarUrl { get; set; }
    public MessageDto? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class ConversationDetailDto : ConversationDto
{
    public List<ConversationParticipantDto> Participants { get; set; } = new();
}

public class ConversationParticipantDto : EntityDto<long>
{
    public long ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string? UserFullName { get; set; }
    public Role Role { get; set; }
    public DateTime? JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public bool IsMuted { get; set; }
    public long? LastReadMessageId { get; set; }
}

public class MessageDto : EntityDto<long>
{
    public long ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderUserName { get; set; } = string.Empty;
    public string? SenderFullName { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; }
    public long? ReplyToMessageId { get; set; }
    public MessageDto? ReplyToMessage { get; set; }
    public DateTime CreationTime { get; set; }
    public List<MessageAttachmentDto> Attachments { get; set; } = new();
}

public class MessageAttachmentDto : EntityDto<long>
{
    public long MessageId { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

public class CreateGroupConversationInput
{
    public string Title { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public List<Guid> MemberIds { get; set; } = new();
}

public class GetConversationsInput : PagedAndSortedResultRequestDto
{
    public string? Filter { get; set; }
    public TypeConversation? Type { get; set; }
}

public class GetMessagesInput : PagedResultRequestDto
{
    public long ConversationId { get; set; }
    public long? BeforeMessageId { get; set; }
}

public class SendMessageInput
{
    public long? ConversationId { get; set; }
    public Guid? TargetUserId { get; set; }
    public long? SubjectId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType MessageType { get; set; } = MessageType.Text;
    public long? ReplyToMessageId { get; set; }
    public List<CreateMessageAttachmentInput>? Attachments { get; set; }
}

public class CreateMessageAttachmentInput
{
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}
