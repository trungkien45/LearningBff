using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using LearningBff.Dtos.Chat;
using LearningBff.Entities;
using LearningBff.Hubs;

namespace LearningBff.Services.Chat;

public class MessageAppService : LearningBffAppService
{
    private readonly IRepository<Conversation, long> _conversationRepository;
    private readonly IRepository<ConversationParticipant, long> _participantRepository;
    private readonly IRepository<Message, long> _messageRepository;
    private readonly IRepository<MessageAttachment, long> _attachmentRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IChatAttachmentStorageService _storageService;
    private readonly IHubContext<ChatHub> _chatHubContext;

    public MessageAppService(
        IRepository<Conversation, long> conversationRepository,
        IRepository<ConversationParticipant, long> participantRepository,
        IRepository<Message, long> messageRepository,
        IRepository<MessageAttachment, long> attachmentRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IChatAttachmentStorageService storageService,
        IHubContext<ChatHub> chatHubContext)
    {
        _conversationRepository = conversationRepository;
        _participantRepository = participantRepository;
        _messageRepository = messageRepository;
        _attachmentRepository = attachmentRepository;
        _userRepository = userRepository;
        _storageService = storageService;
        _chatHubContext = chatHubContext;
    }

    public async Task<PagedResultDto<MessageDto>> GetMessagesAsync(GetMessagesInput input)
    {
        var currentUserId = CurrentUser.GetId();
        var messages = await _messageRepository.GetQueryableAsync();
        var participants = await _participantRepository.GetQueryableAsync();
        var conversationMessages = messages.Where(m => m.ConversationId == input.ConversationId &&
            participants.Any(p => p.ConversationId == m.ConversationId && p.UserId == currentUserId && p.LeftAt == null));
        var totalCount = await conversationMessages.CountAsync();
        var query = conversationMessages.OrderByDescending(m => m.CreationTime).Select(m => new MessageDto
                    {
                        Id = m.Id,
                        ConversationId = m.ConversationId,
                        SenderId = m.SenderId,
                        SenderUserName = m.User!.UserName,
                        SenderFullName = $"{m.User.Surname} {m.User.Name}".Trim(),
                        Content = m.Content,
                        MessageType = m.MessageType,
                        ReplyToMessageId = m.ReplyToMessageId,
                        CreationTime = m.CreationTime
                    });
        var pagedMessages = await query.Skip(input.SkipCount).Take(input.MaxResultCount).ToListAsync();
        return new PagedResultDto<MessageDto>(totalCount, pagedMessages);
    }

    public async Task<MessageDto> SendMessageAsync(SendMessageInput input)
    {
        var currentUserId = CurrentUser.GetId();

        // 1. Validate nội dung tin nhắn và file đính kèm
        var hasContent = !string.IsNullOrWhiteSpace(input.Content);
        var hasFiles = input.Files != null && input.Files.Count > 0;
        var hasBase64Attachments = input.Attachments != null && input.Attachments.Count > 0;
        if (!hasContent && !hasFiles && !hasBase64Attachments)
        {
            throw new UserFriendlyException("Nội dung tin nhắn hoặc tệp đính kèm không được để trống.");
        }

        // 2. Xác định hoặc khởi tạo ConversationId
        var conversationId = await GetOrCreateConversationIdAsync(input, currentUserId);

        // 3. Tạo Entity Message và lưu vào Database
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = currentUserId,
            Content = input.Content?.Trim() ?? string.Empty,
            MessageType = input.MessageType,
            ReplyToMessageId = input.ReplyToMessageId
        };

        await _messageRepository.InsertAsync(message, autoSave: true);

        // 4. Lưu danh sách tệp đính kèm ra bộ nhớ cục bộ
        var attachmentDtos = new List<MessageAttachmentDto>();
        var attachmentsToInsert = new List<MessageAttachment>();

        // 4a. Xử lý lưu IFormFile (Multipart Form Data)
        if (hasFiles)
        {
            foreach (var file in input.Files!)
            {
                if (file.Length == 0) continue;

                using var stream = file.OpenReadStream();
                var blobName = await _storageService.SaveAttachmentAsync(stream, file.FileName);
                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant() ?? string.Empty;

                attachmentsToInsert.Add(new MessageAttachment
                {
                    MessageId = message.Id,
                    BlobName = blobName,
                    FileType = ext,
                    FileName = file.FileName,
                    FileSize = file.Length,
                    ContentType = file.ContentType ?? "application/octet-stream"
                });
            }
        }

        // 4b. Xử lý lưu FileBytes (nếu gửi dạng byte[] / base64 trong JSON)
        if (hasBase64Attachments)
        {
            foreach (var item in input.Attachments!)
            {
                if (item.FileBytes != null && item.FileBytes.Length > 0)
                {
                    using var stream = new MemoryStream(item.FileBytes);
                    var blobName = await _storageService.SaveAttachmentAsync(stream, item.FileName);
                    var ext = Path.GetExtension(item.FileName)?.ToLowerInvariant() ?? string.Empty;

                    attachmentsToInsert.Add(new MessageAttachment
                    {
                        MessageId = message.Id,
                        BlobName = blobName,
                        FileType = ext,
                        FileName = item.FileName,
                        FileSize = item.FileBytes.Length,
                        ContentType = item.ContentType ?? "application/octet-stream"
                    });
                }
            }
        }

        if (attachmentsToInsert.Count > 0)
        {
            await _attachmentRepository.InsertManyAsync(attachmentsToInsert, autoSave: true);

            // Cập nhật FileUrl theo AttachmentId bảo mật: /api/chat/attachments/{id}
            foreach (var a in attachmentsToInsert)
            {
                a.FileUrl = $"/api/chat/attachments/{a.Id}";
            }
            await _attachmentRepository.UpdateManyAsync(attachmentsToInsert, autoSave: true);

            attachmentDtos = attachmentsToInsert.Select(a => new MessageAttachmentDto
            {
                Id = a.Id,
                MessageId = a.MessageId,
                FileUrl = a.FileUrl,
                FileType = a.FileType,
                FileName = a.FileName,
                FileSize = a.FileSize,
                ContentType = a.ContentType
            }).ToList();
        }

        // 5. Lấy thông tin tin nhắn được reply (nếu có)
        MessageDto? replyToMessageDto = null;
        if (message.ReplyToMessageId.HasValue)
        {
            var replyMessage = await (await _messageRepository.GetQueryableAsync())
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == message.ReplyToMessageId.Value);

            if (replyMessage != null)
            {
                replyToMessageDto = new MessageDto
                {
                    Id = replyMessage.Id,
                    ConversationId = replyMessage.ConversationId,
                    SenderId = replyMessage.SenderId,
                    SenderUserName = replyMessage.User?.UserName ?? string.Empty,
                    SenderFullName = replyMessage.User != null ? $"{replyMessage.User.Surname} {replyMessage.User.Name}".Trim() : null,
                    Content = replyMessage.Content,
                    MessageType = replyMessage.MessageType,
                    CreationTime = replyMessage.CreationTime
                };
            }
        }

        // 6. Tạo MessageDto hoàn chỉnh
        var senderFullName = $"{CurrentUser.SurName} {CurrentUser.Name}".Trim();
        var messageDto = new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = message.SenderId,
            SenderUserName = CurrentUser.UserName ?? string.Empty,
            SenderFullName = string.IsNullOrWhiteSpace(senderFullName) ? null : senderFullName,
            Content = message.Content,
            MessageType = message.MessageType,
            ReplyToMessageId = message.ReplyToMessageId,
            ReplyToMessage = replyToMessageDto,
            CreationTime = message.CreationTime,
            Attachments = attachmentDtos
        };

        // 7. Bắn socket qua SignalR Hub tới group của Conversation
        var groupName = $"conversation-{conversationId}";
        await _chatHubContext.Clients.Group(groupName).SendAsync("ReceiveMessage", messageDto);

        // 8. Nếu là tin nhắn đầu tiên khi vừa tạo phòng chat 1-1, gửi thông báo cá nhân tới đối phương
        if (input.TargetUserId.HasValue)
        {
            await _chatHubContext.Clients.User(input.TargetUserId.Value.ToString())
                .SendAsync("NewConversationMessage", messageDto);
        }

        return messageDto;
    }

    private async Task<long> GetOrCreateConversationIdAsync(SendMessageInput input, Guid currentUserId)
    {
        // TH1: Đã có ConversationId
        if (input.ConversationId.HasValue && input.ConversationId.Value > 0)
        {
            var isParticipant = await (await _participantRepository.GetQueryableAsync())
                .AnyAsync(x => x.ConversationId == input.ConversationId.Value
                            && x.UserId == currentUserId
                            && x.LeftAt == null);

            if (!isParticipant)
            {
                throw new UserFriendlyException("Bạn không phải thành viên của cuộc trò chuyện này hoặc đã rời nhóm.");
            }

            return input.ConversationId.Value;
        }

        // TH2: Chưa có ConversationId nhưng có TargetUserId (Bắt đầu chat 1-1)
        if (input.TargetUserId.HasValue)
        {
            var targetUserId = input.TargetUserId.Value;
            if (targetUserId == currentUserId)
            {
                throw new UserFriendlyException("Không thể tạo cuộc trò chuyện với chính mình.");
            }

            var targetUserExists = await (await _userRepository.GetQueryableAsync())
                .AnyAsync(x => x.Id == targetUserId);
            if (!targetUserExists)
            {
                throw new UserFriendlyException("Người dùng cần nhắn tin không tồn tại.");
            }

            // Tìm xem 2 người đã từng có Conversation loại Private chưa
            var convQuery = await _conversationRepository.GetQueryableAsync();
            var partQuery = await _participantRepository.GetQueryableAsync();

            var existingConversationId = await (from c in convQuery
                                                where c.Type == TypeConversation.Private
                                                where partQuery.Any(p => p.ConversationId == c.Id && p.UserId == currentUserId && p.LeftAt == null)
                                                   && partQuery.Any(p => p.ConversationId == c.Id && p.UserId == targetUserId && p.LeftAt == null)
                                                select c.Id).FirstOrDefaultAsync();

            if (existingConversationId > 0)
            {
                return existingConversationId;
            }

            // Nếu chưa có, tạo mới Conversation Private
            var newConv = new Conversation
            {
                Type = TypeConversation.Private,
                Title = string.Empty,
                SubjectId = input.SubjectId
            };
            await _conversationRepository.InsertAsync(newConv, autoSave: true);

            // Thêm cả 2 người vào ConversationParticipant
            var participants = new List<ConversationParticipant>
            {
                new()
                {
                    ConversationId = newConv.Id,
                    UserId = currentUserId,
                    Role = Role.User,
                    JoinedAt = DateTime.UtcNow
                },
                new()
                {
                    ConversationId = newConv.Id,
                    UserId = targetUserId,
                    Role = Role.User,
                    JoinedAt = DateTime.UtcNow
                }
            };
            await _participantRepository.InsertManyAsync(participants, autoSave: true);

            return newConv.Id;
        }

        throw new UserFriendlyException("Vui lòng cung cấp ConversationId hoặc TargetUserId để gửi tin nhắn.");
    }

    public async Task MarkAsReadAsync(long conversationId, long lastReadMessageId)
    {
        var currentUserId = CurrentUser.GetId();
        var conversationParticipant = await _participantRepository.FirstOrDefaultAsync(p => p.ConversationId == conversationId && p.UserId == currentUserId);
        if (conversationParticipant == null || conversationParticipant.LeftAt != null)
        {
            throw new UserFriendlyException("Bạn không phải thành viên của cuộc trò chuyện này.");
        }
        var message = await _messageRepository.FirstOrDefaultAsync(m => m.Id == lastReadMessageId && m.ConversationId == conversationId);
        if (message == null)
        {
            throw new UserFriendlyException("Tin nhắn không tồn tại trong cuộc trò chuyện này.");
        }
        var lastReadMessage = await _messageRepository.FirstOrDefaultAsync(m => m.Id == conversationParticipant.LastReadMessageId && m.ConversationId == conversationId);
        if (lastReadMessage != null && lastReadMessage.CreationTime >= message.CreationTime)
        {
            return; // Không cần cập nhật nếu tin nhắn đã đọc mới hơn hoặc bằng
        }
        conversationParticipant.LastReadMessageId = lastReadMessageId;
        await _participantRepository.UpdateAsync(conversationParticipant, autoSave: true);
    }

    public async Task RecallMessageAsync(long messageId)
    {
        if (messageId <= 0)
        {
            throw new UserFriendlyException("Mã tin nhắn không hợp lệ.");
        }
        var currentUserId = CurrentUser.GetId();
        var message = await _messageRepository.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == currentUserId);
        if (message == null)
        {
            throw new UserFriendlyException("Tin nhắn không tồn tại hoặc không phải do bạn gửi.");
        }
        if (message.CreationTime.AddMinutes(5) < DateTime.UtcNow)
        {
            throw new UserFriendlyException("Chỉ có thể thu hồi tin nhắn trong vòng 5 phút kể từ khi gửi.");
        }
        message.MessageType = MessageType.Recall;
        await _messageRepository.UpdateAsync(message, autoSave: true);
    }
}
