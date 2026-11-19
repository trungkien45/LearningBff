using Volo.Abp.Application.Dtos;
using LearningBff.Dtos.Chat;
using LearningBff.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Identity;
using Volo.Abp;
using Volo.Abp.Users;

namespace LearningBff.Services.Chat;

public class ConversationAppService : LearningBffAppService
{
    private readonly IRepository<Conversation, long> _conversationRepository;
    private readonly IRepository<ConversationParticipant, long> _participantRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<Subject, long> _subjectRepository;

    public ConversationAppService(
        IRepository<Conversation, long> conversationRepository,
        IRepository<ConversationParticipant, long> participantRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<Subject, long> subjectRepository)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _userRepository = userRepository;
        _subjectRepository = subjectRepository;
    }

    public async Task<PagedResultDto<ConversationDto>> GetConversationsAsync(GetConversationsInput input)
    {
        var userId = CurrentUser.Id;
        var query = (await _conversationRepository.GetQueryableAsync())
            .Where(c => c.Participants.Any(p => p.UserId == userId));
        query = input.Type.HasValue ? query.Where(c => c.Type == input.Type.Value) : query;
        query = input.Filter.IsNullOrWhiteSpace() ? query : query.Where(c => c.Title.Contains(input.Filter));
        var totalCount = await query.CountAsync();
        var conversations = await query.Skip(input.SkipCount).Take(input.MaxResultCount).Select(c => new ConversationDto
        {
            Id = c.Id,
            Title = c.Title,
            Type = c.Type,
            SubjectId = c.SubjectId
        }).ToListAsync();

        return new PagedResultDto<ConversationDto>(totalCount, conversations);
    }

    public async Task<ConversationDetailDto> GetConversationDetailAsync(long conversationId)
    {
        var userId = CurrentUser.Id;
        var query = (await _conversationRepository.GetQueryableAsync())
            .Where(c => c.Participants.Any(p => p.UserId == userId && p.ConversationId == conversationId));
        if (!await query.AnyAsync())
        {
            throw new AbpAuthorizationException("You are not a participant of this conversation.");
        }
        query = query.Include(c => c.Participants).ThenInclude(p => p.User);
        query = query.Include(c => c.Messages).ThenInclude(m => m.Attachments);
        var result = await query.Select(c => new ConversationDetailDto
        {
            Id = c.Id,
            Title = c.Title,
            Type = c.Type,
            SubjectId = c.SubjectId,
            Participants = c.Participants.Select(p => new ConversationParticipantDto
            {
                Id = p.Id,
                ConversationId = p.ConversationId,
                UserId = p.UserId,
                UserName = p.User!.UserName,
                UserFullName = p.User.Name,
                Role = p.Role,
                JoinedAt = p.JoinedAt,
                LeftAt = p.LeftAt,
                IsMuted = p.IsMuted,
                LastReadMessageId = p.LastReadMessageId
            }).ToList(),
            LastMessage = c.Messages.OrderByDescending(m => m.CreationTime).Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderUserName = m.User!.UserName,
                SenderFullName = m.User.Name,
                Content = m.Content,
                MessageType = m.MessageType,
                ReplyToMessageId = m.ReplyToMessageId,
                CreationTime = m.CreationTime
            }).FirstOrDefault()
        }).FirstOrDefaultAsync(p => p.Id == conversationId);
        if (result == null)
        {
            throw new EntityNotFoundException<Conversation>("Conversation not found.");
        }
        return result;
    }

    public async Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title))
        {
            throw new UserFriendlyException("Tên nhóm không được để trống.");
        }

        var currentUserId = CurrentUser.Id!.Value;
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
            Role = Role.User,
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

    public async Task<ConversationDto> GetOrCreateSubjectConversationAsync(long subjectId)
    {
        if (subjectId <= 0)
        {
            throw new UserFriendlyException("Mã môn học không hợp lệ.");
        }

        var currentUserId = CurrentUser.Id!.Value;
        var subject = await (await _subjectRepository.GetQueryableAsync())
            .Include(s => s.Teachers)
            .Include(s => s.EnrollmentSubjects)
            .FirstOrDefaultAsync(s => s.Id == subjectId);
        if (subject == null || !subject.IsActive)
        {
            throw new UserFriendlyException("Không tìm thấy môn học hoặc môn học đã ngừng hoạt động.");
        }

        var isTeacher = subject.Teachers.Any(t => t.Id == currentUserId);
        var isEnrolledStudent = subject.EnrollmentSubjects.Any(e => e.UserId == currentUserId && e.IsActive);
        if (!isTeacher && !isEnrolledStudent)
        {
            throw new AbpAuthorizationException("Bạn không có quyền tham gia cuộc trò chuyện của môn học này.");
        }

        var conversationQuery = await _conversationRepository.GetQueryableAsync();
        var conversation = await conversationQuery
            .FirstOrDefaultAsync(c => c.SubjectId == subjectId && c.Type == TypeConversation.Group);
        var teacherIds = subject.Teachers.Select(t => t.Id).ToHashSet();

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

    public Task<ConversationParticipantDto> AddMemberAsync(long conversationId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task RemoveMemberAsync(long conversationId, Guid userId)
    {
        throw new NotImplementedException();
    }

    public Task LeaveConversationAsync(long conversationId)
    {
        throw new NotImplementedException();
    }

    public Task ToggleMuteConversationAsync(long conversationId)
    {
        throw new NotImplementedException();
    }
}
