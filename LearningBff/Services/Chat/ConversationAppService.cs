using Volo.Abp.Application.Dtos;
using LearningBff.Dtos.Chat;

namespace LearningBff.Services.Chat;

public class ConversationAppService : LearningBffAppService
{
    public Task<PagedResultDto<ConversationDto>> GetConversationsAsync(GetConversationsInput input)
    {
        throw new NotImplementedException();
    }

    public Task<ConversationDetailDto> GetConversationDetailAsync(long conversationId)
    {
        throw new NotImplementedException();
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
