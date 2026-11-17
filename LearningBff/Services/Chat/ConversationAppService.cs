using Volo.Abp.Application.Dtos;
using LearningBff.Dtos.Chat;
using LearningBff.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;

namespace LearningBff.Services.Chat;

public class ConversationAppService : LearningBffAppService
{
    private readonly IRepository<Conversation, long> _conversationRepository;

    public ConversationAppService(IRepository<Conversation, long> conversationRepository)
    {
        _conversationRepository = conversationRepository;
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

    public Task<ConversationDto> CreateGroupConversationAsync(CreateGroupConversationInput input)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationDto> GetOrCreateSubjectConversationAsync(long subjectId)
    {
        throw new NotImplementedException();
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
