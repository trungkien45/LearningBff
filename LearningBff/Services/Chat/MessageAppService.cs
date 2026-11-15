using Volo.Abp.Application.Dtos;
using LearningBff.Dtos.Chat;

namespace LearningBff.Services.Chat;

public class MessageAppService : LearningBffAppService
{
    public Task<PagedResultDto<MessageDto>> GetMessagesAsync(GetMessagesInput input)
    {
        throw new NotImplementedException();
    }

    public Task<MessageDto> SendMessageAsync(SendMessageInput input)
    {
        throw new NotImplementedException();
    }

    public Task MarkAsReadAsync(long conversationId, long lastReadMessageId)
    {
        throw new NotImplementedException();
    }

    public Task RecallMessageAsync(long messageId)
    {
        throw new NotImplementedException();
    }
}
