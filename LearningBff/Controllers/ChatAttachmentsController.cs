using System.Threading.Tasks;
using LearningBff.Entities;
using LearningBff.Services.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace LearningBff.Controllers;

[Route("api/chat/attachments")]
[Authorize]
public class ChatAttachmentsController : AbpControllerBase
{
    private readonly IRepository<MessageAttachment, long> _attachmentRepository;
    private readonly IRepository<ConversationParticipant, long> _participantRepository;
    private readonly IChatAttachmentStorageService _storageService;

    public ChatAttachmentsController(
        IRepository<MessageAttachment, long> attachmentRepository,
        IRepository<ConversationParticipant, long> participantRepository,
        IChatAttachmentStorageService storageService)
    {
        _attachmentRepository = attachmentRepository;
        _participantRepository = participantRepository;
        _storageService = storageService;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetAsync(long id)
    {
        var currentUserId = CurrentUser.GetId();

        // 1. Lấy thông tin attachment và message tương ứng
        var attachment = await (await _attachmentRepository.GetQueryableAsync())
            .Include(a => a.Message)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (attachment == null || attachment.Message == null)
        {
            return NotFound("Tệp đính kèm không tồn tại.");
        }

        // 2. Bảo mật: Kiểm tra xem User hiện tại có phải thành viên của cuộc trò chuyện chứa file này không
        var conversationId = attachment.Message.ConversationId;
        var isParticipant = await (await _participantRepository.GetQueryableAsync())
            .AnyAsync(p => p.ConversationId == conversationId
                        && p.UserId == currentUserId
                        && p.LeftAt == null);

        if (!isParticipant)
        {
            throw new UserFriendlyException("Bạn không có quyền truy cập tệp đính kèm này.");
        }

        // 3. Đọc file từ thư mục cục bộ theo BlobName
        var stream = await _storageService.GetAttachmentStreamAsync(attachment.BlobName);
        if (stream == null)
        {
            return NotFound("Tệp không tồn tại trên hệ thống lưu trữ.");
        }

        var contentType = string.IsNullOrWhiteSpace(attachment.ContentType)
            ? "application/octet-stream"
            : attachment.ContentType;

        Response.Headers.Append("Cache-Control", "private, max-age=86400");
        return File(stream, contentType, attachment.FileName, enableRangeProcessing: true);
    }
}
