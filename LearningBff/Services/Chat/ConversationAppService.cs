using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using LearningBff.Dtos.Chat;
using LearningBff.Entities;
using LearningBff.Hubs;

namespace LearningBff.Services.Chat;

public class ConversationAppService : LearningBffAppService
{
    private readonly IRepository<Conversation, long> _conversationRepository;
    private readonly IRepository<ConversationParticipant, long> _participantRepository;
    private readonly IRepository<Message, long> _messageRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<Subject, long> _subjectRepository;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public ConversationAppService(
        IRepository<Conversation, long> conversationRepository,
        IRepository<ConversationParticipant, long> participantRepository,
        IRepository<Message, long> messageRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Subject, long> subjectRepository,
        IHubContext<ChatHub> chatHubContext)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _subjectRepository = subjectRepository;
        _chatHubContext = chatHubContext;
    }

    public async Task<PagedResultDto<ConversationDto>> GetConversationsAsync(GetConversationsInput input)
    {
        var userId = CurrentUser.GetId();
        var query = (await _conversationRepository.GetQueryableAsync())
            .Where(c => c.Participants.Any(p => p.UserId == userId && p.LeftAt == null));

        if (input.Type.HasValue)
        {
            query = query.Where(c => c.Type == input.Type.Value);
        }

        if (!input.Filter.IsNullOrWhiteSpace())
        {
            query = query.Where(c => c.Title.Contains(input.Filter));
        }

        var totalCount = await query.CountAsync();
        var conversations = await query
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Title,
                Type = c.Type,
                SubjectId = c.SubjectId,
                AvatarUrl = c.AvatarUrl
            }).ToListAsync();

        return new PagedResultDto<ConversationDto>(totalCount, conversations);
    }

    public async Task<ConversationDetailDto> GetConversationDetailAsync(long conversationId)
    {
        var userId = CurrentUser.GetId();
        var query = (await _conversationRepository.GetQueryableAsync())
            .Where(c => c.Id == conversationId && c.Participants.Any(p => p.UserId == userId && p.LeftAt == null))
            .Include(c => c.Participants).ThenInclude(p => p.User)
            .Include(c => c.Messages).ThenInclude(m => m.Attachments);

        var result = await query.Select(c => new ConversationDetailDto
        {
            Id = c.Id,
            Title = c.Title,
            Type = c.Type,
            SubjectId = c.SubjectId,
            AvatarUrl = c.AvatarUrl,
            Participants = c.Participants
                .Where(p => p.LeftAt == null)
                .Select(p => new ConversationParticipantDto
                {
                    Id = p.Id,
                    ConversationId = p.ConversationId,
                    UserId = p.UserId,
                    UserName = p.User != null ? p.User.UserName : string.Empty,
                    UserFullName = p.User != null ? $"{p.User.Surname} {p.User.Name}".Trim() : null,
                    Role = p.Role,
                    JoinedAt = p.JoinedAt,
                    LeftAt = p.LeftAt,
                    IsMuted = p.IsMuted,
                    LastReadMessageId = p.LastReadMessageId
                }).ToList(),
            LastMessage = c.Messages
                .OrderByDescending(m => m.CreationTime)
                .Select(m => new MessageDto
                {
                    Id = m.Id,
                    ConversationId = m.ConversationId,
                    SenderId = m.SenderId,
                    SenderUserName = m.User != null ? m.User.UserName : string.Empty,
                    SenderFullName = m.User != null ? $"{m.User.Surname} {m.User.Name}".Trim() : null,
                    Content = m.Content,
                    MessageType = m.MessageType,
                    ReplyToMessageId = m.ReplyToMessageId,
                    CreationTime = m.CreationTime
                }).FirstOrDefault()
        }).FirstOrDefaultAsync();

        if (result == null)
        {
            throw new EntityNotFoundException(typeof(Conversation), conversationId);
        }

        return result;
    }

    public async Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("Tên nhóm không được để trống.");
        }

        var currentUserId = CurrentUser.GetId();
        var memberIds = input.MemberIds
            .Where(id => id != Guid.Empty && id != currentUserId)
            .Distinct()
            .ToList();
        var allMemberIds = memberIds.Append(currentUserId).ToList();

        var users = await _userRepository.GetListAsync(user => allMemberIds.Contains(user.Id));
        if (users.Count != allMemberIds.Count)
        {
            throw new UserFriendlyException("Một hoặc nhiều thành viên không tồn tại.");
        }

        var conversation = new Conversation
        {
            Title = input.Title.Trim(),
            AvatarUrl = input.AvatarUrl,
            Type = TypeConversation.Group
        };
        await _conversationRepository.InsertAsync(conversation, autoSave: true);

        var participants = allMemberIds.Select(userId => new ConversationParticipant
        {
            ConversationId = conversation.Id,
            UserId = userId,
            Role = currentUserId == userId ? Role.Admin : Role.User,
            JoinedAt = DateTime.UtcNow
        }).ToList();
        await _participantRepository.InsertManyAsync(participants, autoSave: true);

        return new ConversationDto
        {
            Id = conversation.Id,
            Title = conversation.Title,
            Type = conversation.Type,
            AvatarUrl = conversation.AvatarUrl
        };
    }

    [Authorize(Roles = LearningBffConsts.Teacher)]
    public async Task<ConversationDto> GetOrCreateSubjectConversationAsync(long subjectId)
    {
        if (subjectId <= 0)
        {
            throw new UserFriendlyException("Mã môn học không hợp lệ.");
        }

        var currentUserId = CurrentUser.GetId();
        var subject = await GetSubjectWithRelationsAsync(subjectId);
        ValidateUserSubjectAccess(subject, currentUserId);

        var conversationQuery = await _conversationRepository.GetQueryableAsync();
        var conversation = await conversationQuery
            .FirstOrDefaultAsync(c => c.SubjectId == subjectId && c.Type == TypeConversation.Group);
        var teacherIds = subject.Teachers.Select(t => t.Id).ToHashSet();

        if (teacherIds.Count == 0)
        {
            throw new UserFriendlyException("Môn học chưa có giáo viên phụ trách. Không thể tạo nhóm thảo luận.");
        }

        if (conversation == null)
        {
            conversation = new Conversation
            {
                SubjectId = subjectId,
                Title = subject.Name,
                Type = TypeConversation.Group
            };
            await _conversationRepository.InsertAsync(conversation, autoSave: true);

            var participantIds = teacherIds
                .Append(currentUserId)
                .Distinct()
                .ToList();
            var participants = participantIds.Select(userId => new ConversationParticipant
            {
                ConversationId = conversation.Id,
                UserId = userId,
                Role = teacherIds.Contains(userId) ? Role.Teacher : Role.User,
                JoinedAt = DateTime.UtcNow
            }).ToList();
            await _participantRepository.InsertManyAsync(participants, autoSave: true);
        }
        else
        {
            var participant = await _participantRepository.FirstOrDefaultAsync(p =>
                p.ConversationId == conversation.Id && p.UserId == currentUserId);
            if (participant == null)
            {
                await _participantRepository.InsertAsync(new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = currentUserId,
                    Role = teacherIds.Contains(currentUserId) ? Role.Teacher : Role.User,
                    JoinedAt = DateTime.UtcNow
                }, autoSave: true);
            }
            else if (participant.LeftAt != null)
            {
                participant.LeftAt = null;
                participant.JoinedAt = DateTime.UtcNow;
                await _participantRepository.UpdateAsync(participant, autoSave: true);
            }
        }

        return new ConversationDto
        {
            Id = conversation.Id,
            SubjectId = conversation.SubjectId,
            SubjectName = subject.Name,
            Title = conversation.Title,
            Type = conversation.Type,
            AvatarUrl = conversation.AvatarUrl
        };
    }

    public async Task<ConversationParticipantDto> AddMemberAsync(long conversationId, Guid userId)
    {
        var conversation = await GetGroupConversationWithParticipantsAsync(conversationId);

        if (conversation.Participants.Any(p => p.UserId == userId && p.LeftAt == null))
        {
            throw new UserFriendlyException("Thành viên đã tham gia cuộc trò chuyện.");
        }

        if (!await _userRepository.AnyAsync(u => u.Id == userId))
        {
            throw new UserFriendlyException("Người dùng không tồn tại.");
        }

        var currentUserId = CurrentUser.GetId();
        if (conversation.SubjectId != null)
        {
            var subject = await GetSubjectWithRelationsAsync(conversation.SubjectId.Value);
            ValidateTeacherManagementAccess(subject, currentUserId);
            ValidateUserSubjectAccess(subject, userId);
        }
        else
        {
            ValidateAdminRole(conversation, currentUserId, "Chỉ admin mới có quyền thêm thành viên vào cuộc trò chuyện này.");
        }

        // Tái kích hoạt nếu từng rời nhóm trước đó
        var existingParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == userId && p.LeftAt != null);
        if (existingParticipant != null)
        {
            existingParticipant.LeftAt = null;
            existingParticipant.JoinedAt = DateTime.UtcNow;
            await _participantRepository.UpdateAsync(existingParticipant, autoSave: true);
            return MapToParticipantDto(existingParticipant);
        }

        var newParticipant = new ConversationParticipant
        {
            ConversationId = conversationId,
            UserId = userId,
            Role = Role.User,
            JoinedAt = DateTime.UtcNow
        };
        await _participantRepository.InsertAsync(newParticipant, autoSave: true);
        return MapToParticipantDto(newParticipant);
    }

    public async Task RemoveMemberAsync(long conversationId, Guid userId)
    {
        var conversation = await GetGroupConversationWithParticipantsAsync(conversationId);
        var targetParticipant = conversation.Participants.FirstOrDefault(p => p.UserId == userId && p.LeftAt == null);
        if (targetParticipant == null)
        {
            throw new UserFriendlyException("Thành viên không tồn tại trong cuộc trò chuyện hoặc đã rời nhóm.");
        }

        var currentUserId = CurrentUser.GetId();
        if (conversation.SubjectId != null)
        {
            var subject = await GetSubjectWithRelationsAsync(conversation.SubjectId.Value);
            ValidateTeacherManagementAccess(subject, currentUserId);
        }
        else
        {
            ValidateAdminRole(conversation, currentUserId, "Chỉ admin mới có quyền gỡ thành viên khỏi cuộc trò chuyện này.");

            if (currentUserId == userId)
            {
                throw new UserFriendlyException("Admin không thể gỡ chính mình khỏi cuộc trò chuyện. Vui lòng chuyển quyền Admin cho một thành viên khác trước khi rời nhóm.");
            }
        }

        targetParticipant.LeftAt = DateTime.UtcNow;
        await _participantRepository.UpdateAsync(targetParticipant, autoSave: true);
    }

    public async Task LeaveConversationAsync(long conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        var conversation = await _conversationRepository.FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conversation == null)
        {
            throw new EntityNotFoundException(typeof(Conversation), conversationId);
        }

        if (conversation.Type == TypeConversation.Private)
        {
            throw new UserFriendlyException("Không thể rời khỏi cuộc trò chuyện cá nhân 1-1.");
        }

        var participant = await (await _participantRepository.GetQueryableAsync())
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && p.LeftAt == null);

        if (participant == null)
        {
            throw new UserFriendlyException("Bạn không phải là thành viên hoặc đã rời khỏi cuộc trò chuyện này.");
        }

        // Nhóm môn học
        if (conversation.SubjectId != null)
        {
            var subject = await GetSubjectWithRelationsAsync(conversation.SubjectId.Value);

            var isTeacher = subject.Teachers.Any(t => t.Id == currentUserId);
            if (isTeacher)
            {
                throw new UserFriendlyException("Giáo viên phụ trách không thể rời khỏi nhóm thảo luận của môn học.");
            }

            var isEnrolledStudent = subject.EnrollmentSubjects.Any(e => e.UserId == currentUserId && e.IsActive);
            if (isEnrolledStudent)
            {
                throw new UserFriendlyException("Học sinh đang theo học môn này không thể rời khỏi nhóm thảo luận. Bạn cần hủy đăng ký môn học trước.");
            }
        }
        else if (participant.Role == Role.Admin)
        {
            var otherActiveParticipants = await GetActiveParticipantsAsync(conversationId, currentUserId);
            if (otherActiveParticipants.Count > 0 && !otherActiveParticipants.Any(p => p.Role == Role.Admin))
            {
                throw new UserFriendlyException("Bạn là Admin duy nhất trong nhóm. Vui lòng chỉ định một thành viên khác làm Admin trước khi rời nhóm.");
            }
        }

        participant.LeftAt = DateTime.UtcNow;
        await _participantRepository.UpdateAsync(participant, autoSave: true);

        // Nhóm tự do không còn ai -> dọn dẹp
        var activeCount = await (await _participantRepository.GetQueryableAsync())
            .CountAsync(p => p.ConversationId == conversationId && p.LeftAt == null);

        if (activeCount == 0 && conversation.SubjectId == null)
        {
            await _conversationRepository.DeleteAsync(conversation, autoSave: true);
        }

        // Tin nhắn hệ thống và thông báo realtime
        var displayName = GetCurrentDisplayName();
        var systemContent = $"{displayName} đã rời khỏi cuộc trò chuyện.";
        var systemMessageDto = await CreateAndSaveSystemMessageAsync(conversationId, currentUserId, displayName, systemContent);

        var groupName = $"conversation-{conversationId}";
        await _chatHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", systemMessageDto);
        await _chatHubContext.Clients.Group(groupName).SendAsync("UserLeftConversation", new
        {
            ConversationId = conversationId,
            UserId = currentUserId,
            UserName = CurrentUser.UserName
        });
    }

    public async Task ToggleMuteConversationAsync(long conversationId)
    {
        var currentUserId = CurrentUser.GetId();
        var participant = await (await _participantRepository.GetQueryableAsync())
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId && p.LeftAt == null);

        if (participant == null)
        {
            throw new UserFriendlyException("Bạn không phải là thành viên của cuộc trò chuyện này.");
        }

        participant.IsMuted = !participant.IsMuted;
        await _participantRepository.UpdateAsync(participant, autoSave: true);
    }

    public async Task SetRoleAsync(long conversationId, Guid userId, Role role)
    {
        var currentUserId = CurrentUser.GetId();
        var conversation = await (await _conversationRepository.GetQueryableAsync())
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            throw new UserFriendlyException("Cuộc trò chuyện không tồn tại.");
        }

        if (conversation.Type == TypeConversation.Private)
        {
            throw new UserFriendlyException("Chỉ có thể thay đổi vai trò trong cuộc trò chuyện nhóm.");
        }

        if (conversation.SubjectId != null)
        {
            throw new UserFriendlyException("Không thể thay đổi vai trò trong cuộc trò chuyện liên quan đến môn học.");
        }

        ValidateAdminRole(conversation, currentUserId, "Bạn không có quyền thay đổi vai trò của người khác.");
        if (role == Role.Teacher)
        {
            throw new UserFriendlyException("Vai trò Teacher chỉ áp dụng cho cuộc trò chuyện nhóm môn học.");
        }
        var targetParticipant = await (await _participantRepository.GetQueryableAsync())
            .FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == userId && p.LeftAt == null);

        if (targetParticipant == null)
        {
            throw new UserFriendlyException("Người dùng không tồn tại hoặc đã rời khỏi cuộc trò chuyện.");
        }

        if (targetParticipant.Role == Role.Admin && role != Role.Admin)
        {
            var otherAdmins = await (await _participantRepository.GetQueryableAsync())
                .Where(p => p.ConversationId == conversationId && p.Role == Role.Admin && p.LeftAt == null && p.UserId != userId)
                .ToListAsync();

            if (!otherAdmins.Any())
            {
                throw new UserFriendlyException("Người dùng này là Admin duy nhất trong nhóm. Vui lòng chuyển quyền Admin cho một thành viên khác trước khi thay đổi vai trò.");
            }
        }

        targetParticipant.Role = role;
        await _participantRepository.UpdateAsync(targetParticipant, autoSave: true);
    }

    #region Private Helper Methods

    private async Task<Conversation> GetGroupConversationWithParticipantsAsync(long conversationId)
    {
        var conversation = await (await _conversationRepository.GetQueryableAsync())
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null)
        {
            throw new UserFriendlyException("Cuộc trò chuyện không tồn tại.");
        }

        if (conversation.Type == TypeConversation.Private)
        {
            throw new UserFriendlyException("Thao tác này chỉ áp dụng cho cuộc trò chuyện nhóm.");
        }

        return conversation;
    }

    private async Task<Subject> GetSubjectWithRelationsAsync(long subjectId)
    {
        var subject = await (await _subjectRepository.GetQueryableAsync())
            .Include(s => s.Teachers)
            .Include(s => s.EnrollmentSubjects)
            .FirstOrDefaultAsync(s => s.Id == subjectId);

        if (subject == null || !subject.IsActive)
        {
            throw new UserFriendlyException("Môn học không tồn tại hoặc đã ngừng hoạt động.");
        }

        return subject;
    }

    private static void ValidateUserSubjectAccess(Subject subject, Guid userId)
    {
        var isTeacher = subject.Teachers.Any(t => t.Id == userId);
        var isEnrolledStudent = subject.EnrollmentSubjects.Any(e => e.UserId == userId && e.IsActive);
        if (!isTeacher && !isEnrolledStudent)
        {
            throw new AbpAuthorizationException("Người dùng không có quyền tham gia cuộc trò chuyện của môn học này.");
        }
    }

    private static void ValidateTeacherManagementAccess(Subject subject, Guid currentUserId)
    {
        if (!subject.Teachers.Any(t => t.Id == currentUserId))
        {
            throw new AbpAuthorizationException("Bạn không có quyền quản lý thành viên trong cuộc trò chuyện của môn học này.");
        }
    }

    private static void ValidateAdminRole(Conversation conversation, Guid currentUserId, string errorMessage)
    {
        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == currentUserId && p.LeftAt == null);
        if (participant == null || participant.Role != Role.Admin)
        {
            throw new AbpAuthorizationException(errorMessage);
        }
    }

    private async Task<List<ConversationParticipant>> GetActiveParticipantsAsync(long conversationId, Guid excludeUserId)
    {
        return await (await _participantRepository.GetQueryableAsync())
            .Where(p => p.ConversationId == conversationId && p.LeftAt == null && p.UserId != excludeUserId)
            .ToListAsync();
    }

    private string GetCurrentDisplayName()
    {
        var name = $"{CurrentUser.SurName} {CurrentUser.Name}".Trim();
        return !string.IsNullOrWhiteSpace(name) ? name : CurrentUser.UserName ?? "Thành viên";
    }

    private async Task<MessageDto> CreateAndSaveSystemMessageAsync(long conversationId, Guid senderId, string senderName, string content)
    {
        var systemMessage = new Message
        {
            ConversationId = conversationId,
            SenderId = senderId,
            Content = content,
            MessageType = MessageType.System
        };
        await _messageRepository.InsertAsync(systemMessage, autoSave: true);

        return new MessageDto
        {
            Id = systemMessage.Id,
            ConversationId = conversationId,
            SenderId = senderId,
            SenderUserName = CurrentUser.UserName ?? string.Empty,
            SenderFullName = senderName,
            Content = content,
            MessageType = MessageType.System,
            CreationTime = systemMessage.CreationTime
        };
    }

    private static ConversationParticipantDto MapToParticipantDto(ConversationParticipant participant)
    {
        return new ConversationParticipantDto
        {
            Id = participant.Id,
            ConversationId = participant.ConversationId,
            UserId = participant.UserId,
            Role = participant.Role,
            JoinedAt = participant.JoinedAt,
            LeftAt = participant.LeftAt,
            IsMuted = participant.IsMuted,
            LastReadMessageId = participant.LastReadMessageId
        };
    }

    #endregion
}
