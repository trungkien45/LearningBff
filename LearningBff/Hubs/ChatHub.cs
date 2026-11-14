using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Volo.Abp.AspNetCore.SignalR;

namespace LearningBff.Hubs;

[Authorize]
public class ChatHub : AbpHub
{
    public async Task JoinConversation(long conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
    }

    public async Task LeaveConversation(long conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroupName(conversationId));
    }

    public async Task Typing(long conversationId, bool isTyping)
    {
        var groupName = GetConversationGroupName(conversationId);
        await Clients.OthersInGroup(groupName).SendAsync("UserTyping", new
        {
            ConversationId = conversationId,
            UserId = CurrentUser.Id,
            UserName = CurrentUser.UserName,
            IsTyping = isTyping
        });
    }

    private static string GetConversationGroupName(long conversationId) => $"conversation-{conversationId}";
}

