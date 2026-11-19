using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LearningBff.Entities;
using LearningBff.Dtos;
using Volo.Abp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace LearningBff.Services;

[Authorize(Roles = LearningBffConsts.Admin)]
public class SubjectAdminAppService : LearningBffAppService
{
    private readonly IRepository<Subject, long> _subjectRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IRepository<ConversationParticipant, long> _conversationParticipantRepository;
    private readonly IRepository<Conversation, long> _conversationRepository;
    private readonly SubjectAppService _subjectAppService;
    private readonly Services.LessonImages.ILessonImageAppService _lessonImageAppService;

    public SubjectAdminAppService(
        IRepository<Subject, long> subjectRepository,
        IRepository<IdentityUser, Guid> userRepository,
        IRepository<ConversationParticipant, long> conversationParticipantRepository,
        IRepository<Conversation, long> conversationRepository,
        SubjectAppService subjectAppService,
        Services.LessonImages.ILessonImageAppService lessonImageAppService)
    {
        _subjectRepository = subjectRepository;
        _userRepository = userRepository;
        _conversationParticipantRepository = conversationParticipantRepository;
        _conversationRepository = conversationRepository;
        _subjectAppService = subjectAppService;
        _lessonImageAppService = lessonImageAppService;
    }

    /// <summary>
    /// Lấy thống kê tổng quan các môn học cho Admin
    /// </summary>
    public async Task<(int Total, int Active, int Assigned, int Unassigned)> GetAdminStatsAsync()
    {
        var list = await (await _subjectRepository.GetQueryableAsync())
            .Select(s => new { s.IsActive, HasTeachers = s.Teachers.Any() })
            .ToListAsync();

        var total = list.Count;
        var active = list.Count(s => s.IsActive);
        var assigned = list.Count(s => s.HasTeachers);
        var unassigned = total - assigned;

        return (total, active, assigned, unassigned);
    }

    /// <summary>
    /// Tạo môn học mới và gán danh sách giảng viên ban đầu
    /// </summary>
    public async Task<SubjectDto> CreateAsync(CreateSubjectDto input)
    {
        var trimmedName = input.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new UserFriendlyException("Tên môn học không được để trống.");
        }

        var exists = await _subjectRepository.AnyAsync(x => x.Name.ToLower() == trimmedName.ToLower());
        if (exists)
        {
            throw new UserFriendlyException($"Môn học '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        var subject = new Subject(trimmedName, input.Description?.Trim(), input.IsActive);

        if (input.TeacherIds != null && input.TeacherIds.Any())
        {
            var teachers = await _userRepository.GetListAsync(u => input.TeacherIds.Contains(u.Id));
            subject.Teachers.AddRange(teachers);
        }

        await _subjectRepository.InsertAsync(subject, autoSave: true);

        return await _subjectAppService.GetAsync(subject.Id);
    }

    /// <summary>
    /// Cập nhật thông tin môn học và giảng viên
    /// </summary>
    public async Task<SubjectDto> UpdateAsync(long id, UpdateSubjectDto input)
    {
        var trimmedName = input.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new UserFriendlyException("Tên môn học không được để trống.");
        }

        var subject = await (await _subjectRepository.GetQueryableAsync())
            .Include(s => s.Teachers)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new UserFriendlyException($"Không tìm thấy môn học với mã {id}.");

        var duplicate = await _subjectRepository.AnyAsync(x => x.Name.ToLower() == trimmedName.ToLower() && x.Id != id);
        if (duplicate)
        {
            throw new UserFriendlyException($"Môn học '{trimmedName}' đã tồn tại trong hệ thống.");
        }

        subject.Name = trimmedName;
        subject.Description = input.Description?.Trim();
        subject.IsActive = input.IsActive;

        // Cập nhật quan hệ giảng viên nếu có truyền vào
        if (input.TeacherIds != null)
        {
            var previousTeacherIds = subject.Teachers.Select(t => t.Id).ToHashSet();
            subject.Teachers.Clear();
            var teachers = new List<IdentityUser>();
            if (input.TeacherIds.Any())
            {
                teachers = await _userRepository.GetListAsync(u => input.TeacherIds.Contains(u.Id));
                subject.Teachers.AddRange(teachers);
            }

            await SyncSubjectConversationTeachersAsync(subject.Id, previousTeacherIds, teachers.Select(t => t.Id).ToHashSet());
        }

        await _subjectRepository.UpdateAsync(subject, autoSave: true);
        return await _subjectAppService.GetAsync(subject.Id);
    }

    /// <summary>
    /// Gán hoặc bỏ gán giảng viên cho môn học
    /// </summary>
    public async Task AssignTeachersAsync(long subjectId, List<Guid> teacherIds)
    {
        var subject = await (await _subjectRepository.GetQueryableAsync())
            .Include(s => s.Teachers)
            .FirstOrDefaultAsync(s => s.Id == subjectId)
            ?? throw new UserFriendlyException($"Không tìm thấy môn học với mã {subjectId}.");

        var previousTeacherIds = subject.Teachers.Select(t => t.Id).ToHashSet();
        var teachers = new List<IdentityUser>();
        subject.Teachers.Clear();
        if (teacherIds != null && teacherIds.Any())
        {
            teachers = await _userRepository.GetListAsync(u => teacherIds.Contains(u.Id));
            subject.Teachers.AddRange(teachers);
        }

        await _subjectRepository.UpdateAsync(subject, autoSave: true);
        await SyncSubjectConversationTeachersAsync(subject.Id, previousTeacherIds, teachers.Select(t => t.Id).ToHashSet());
    }

    private async Task SyncSubjectConversationTeachersAsync(long subjectId, HashSet<Guid> previousTeacherIds, HashSet<Guid> currentTeacherIds)
    {
        var conversation = await (await _conversationRepository.GetQueryableAsync())
            .Where(c => c.SubjectId == subjectId && c.Type == TypeConversation.Group)
            .FirstOrDefaultAsync();
        if (conversation == null)
        {
            return;
        }

        var participants = await _conversationParticipantRepository.GetListAsync(p => p.ConversationId == conversation.Id);
        var now = DateTime.UtcNow;

        foreach (var participant in participants.Where(p => previousTeacherIds.Contains(p.UserId) && !currentTeacherIds.Contains(p.UserId) && p.LeftAt == null))
        {
            participant.LeftAt = now;
            await _conversationParticipantRepository.UpdateAsync(participant);
        }

        foreach (var teacherId in currentTeacherIds.Except(previousTeacherIds))
        {
            var participant = participants.FirstOrDefault(p => p.UserId == teacherId);
            if (participant == null)
            {
                await _conversationParticipantRepository.InsertAsync(new ConversationParticipant
                {
                    ConversationId = conversation.Id,
                    UserId = teacherId,
                    Role = Role.Teacher,
                    JoinedAt = now
                });
            }
            else
            {
                participant.Role = Role.Teacher;
                participant.JoinedAt = now;
                participant.LeftAt = null;
                await _conversationParticipantRepository.UpdateAsync(participant);
            }
        }
    }

    /// <summary>
    /// Xóa môn học
    /// </summary>
    public async Task DeleteAsync(long id)
    {
        var subject = await (await _subjectRepository.GetQueryableAsync())
            .Include(s => s.Teachers)
            .FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new UserFriendlyException($"Không tìm thấy môn học với mã {id}.");

        await _lessonImageAppService.DeleteSubjectImagesAsync(id);
        await _subjectRepository.DeleteAsync(subject, autoSave: true);
    }
}
