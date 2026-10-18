using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LearningBff.Dtos;
using LearningBff.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace LearningBff.Services.LessonImages;

public class LessonImageAppService : LearningBffAppService, ILessonImageAppService
{
    private static readonly Regex ImageIdRegex = new(
        @"/api/lesson-images/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IRepository<LessonImage, Guid> _lessonImageRepository;
    private readonly IRepository<Lesson, long> _lessonRepository;
    private readonly IRepository<Chapter, long> _chapterRepository;
    private readonly IRepository<Subject, long> _subjectRepository;
    private readonly IRepository<EnrollmentSubject, long> _enrollmentSubjectRepository;
    private readonly ILessonImageStorageService _storageService;
    private readonly ICurrentUser _currentUser;

    public LessonImageAppService(
        IRepository<LessonImage, Guid> lessonImageRepository,
        IRepository<Lesson, long> lessonRepository,
        IRepository<Chapter, long> chapterRepository,
        IRepository<Subject, long> subjectRepository,
        IRepository<EnrollmentSubject, long> enrollmentSubjectRepository,
        ILessonImageStorageService storageService,
        ICurrentUser currentUser)
    {
        _lessonImageRepository = lessonImageRepository;
        _lessonRepository = lessonRepository;
        _chapterRepository = chapterRepository;
        _subjectRepository = subjectRepository;
        _enrollmentSubjectRepository = enrollmentSubjectRepository;
        _storageService = storageService;
        _currentUser = currentUser;
    }

    public async Task<LessonImageUploadResultDto> UploadAsync(IFormFile file, long? chapterId, long? lessonId)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AbpAuthorizationException("Vui lòng đăng nhập để thực hiện tải ảnh.");
        }

        if (file == null || file.Length == 0)
        {
            throw new UserFriendlyException("Vui lòng chọn file ảnh để tải lên.");
        }

        Subject? subject = null;
        if (lessonId.HasValue && lessonId.Value > 0)
        {
            var lesson = await (await _lessonRepository.GetQueryableAsync())
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.Subject)
                        .ThenInclude(s => s.Teachers)
                .FirstOrDefaultAsync(l => l.Id == lessonId.Value);

            if (lesson == null)
            {
                throw new UserFriendlyException("Không tìm thấy bài học tương ứng.");
            }

            subject = lesson.Chapter.Subject;
            chapterId ??= lesson.ChapterId;
        }
        else if (chapterId.HasValue && chapterId.Value > 0)
        {
            var chapter = await (await _chapterRepository.GetQueryableAsync())
                .Include(c => c.Subject)
                    .ThenInclude(s => s.Teachers)
                .FirstOrDefaultAsync(c => c.Id == chapterId.Value);

            if (chapter == null)
            {
                throw new UserFriendlyException("Không tìm thấy chương học tương ứng.");
            }

            subject = chapter.Subject;
        }
        else
        {
            throw new UserFriendlyException("Cần cung cấp mã chương học hoặc bài học để tải ảnh.");
        }

        var userId = _currentUser.Id;
        var isAdmin = _currentUser.IsInRole(LearningBffConsts.Admin);
        var isTeacher = _currentUser.IsInRole(LearningBffConsts.Teacher)
                        && userId.HasValue
                        && subject.Teachers.Any(t => t.Id == userId.Value);

        if (!isAdmin && !isTeacher)
        {
            throw new AbpAuthorizationException("Bạn không có quyền tải ảnh lên môn học này.");
        }

        using (var readStream = file.OpenReadStream())
        {
            LessonImageValidator.Validate(readStream, file.FileName, file.ContentType, file.Length);

            var extension = Path.GetExtension(file.FileName);
            var contentType = LessonImageValidator.NormalizeContentType(extension, file.ContentType);
            var blobName = await _storageService.SaveBlobAsync(readStream, extension);

            var image = new LessonImage(
                id: Guid.NewGuid(),
                blobName: blobName,
                originalFileName: Path.GetFileName(file.FileName),
                contentType: contentType,
                fileSize: file.Length,
                lessonId: lessonId,
                chapterId: chapterId);

            await _lessonImageRepository.InsertAsync(image, autoSave: true);

            return new LessonImageUploadResultDto
            {
                Id = image.Id,
                Url = $"/api/lesson-images/{image.Id}",
                FileName = image.OriginalFileName,
                FileSize = image.FileSize,
                ContentType = image.ContentType
            };
        }
    }

    public async Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(Guid id)
    {
        var image = await (await _lessonImageRepository.GetQueryableAsync())
            .Include(i => i.Lesson)
                .ThenInclude(l => l!.Chapter)
                    .ThenInclude(c => c.Subject)
                        .ThenInclude(s => s.Teachers)
            .Include(i => i.Chapter)
                .ThenInclude(c => c!.Subject)
                    .ThenInclude(s => s.Teachers)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (image == null)
        {
            throw new UserFriendlyException("Không tìm thấy thông tin ảnh.");
        }

        var subject = image.Lesson?.Chapter?.Subject ?? image.Chapter?.Subject;
        if (subject == null)
        {
            throw new UserFriendlyException("Không tìm thấy môn học liên kết với ảnh này.");
        }

        if (!_currentUser.IsAuthenticated)
        {
            throw new AbpAuthorizationException("Vui lòng đăng nhập để xem nội dung bài học.");
        }

        var userId = _currentUser.Id;
        var isAdmin = _currentUser.IsInRole(LearningBffConsts.Admin);
        var isTeacher = _currentUser.IsInRole(LearningBffConsts.Teacher)
                        && userId.HasValue
                        && subject.Teachers.Any(t => t.Id == userId.Value);

        var isStudent = false;
        if (!isAdmin && !isTeacher && userId.HasValue)
        {
            isStudent = await _enrollmentSubjectRepository.AnyAsync(e =>
                e.UserId == userId.Value && e.SubjectId == subject.Id && e.IsActive);
        }

        if (!isAdmin && !isTeacher && !isStudent)
        {
            throw new AbpAuthorizationException("Bạn không có quyền truy cập ảnh bài học này.");
        }

        var stream = await _storageService.GetBlobStreamAsync(image.BlobName);
        if (stream == null)
        {
            throw new UserFriendlyException("File ảnh không tồn tại trên hệ thống lưu trữ.");
        }

        return (stream, image.ContentType, image.OriginalFileName);
    }

    public async Task DeleteAsync(Guid id)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new AbpAuthorizationException("Vui lòng đăng nhập để thực hiện xóa ảnh.");
        }

        var image = await (await _lessonImageRepository.GetQueryableAsync())
            .Include(i => i.Lesson)
                .ThenInclude(l => l!.Chapter)
                    .ThenInclude(c => c.Subject)
                        .ThenInclude(s => s.Teachers)
            .Include(i => i.Chapter)
                .ThenInclude(c => c!.Subject)
                    .ThenInclude(s => s.Teachers)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (image == null)
        {
            return;
        }

        var subject = image.Lesson?.Chapter?.Subject ?? image.Chapter?.Subject;
        if (subject != null)
        {
            var userId = _currentUser.Id;
            var isAdmin = _currentUser.IsInRole(LearningBffConsts.Admin);
            var isTeacher = _currentUser.IsInRole(LearningBffConsts.Teacher)
                            && userId.HasValue
                            && subject.Teachers.Any(t => t.Id == userId.Value);

            if (!isAdmin && !isTeacher)
            {
                throw new AbpAuthorizationException("Bạn không có quyền xóa ảnh của môn học này.");
            }
        }

        await _storageService.DeleteBlobAsync(image.BlobName);
        await _lessonImageRepository.DeleteAsync(image, autoSave: true);
    }

    public async Task SyncLessonImagesAsync(long lessonId, string? content, long? chapterId = null)
    {
        var referencedGuids = ExtractImageGuids(content);

        // 1. Link images in content to this lesson
        if (referencedGuids.Any())
        {
            var imagesToLink = await (await _lessonImageRepository.GetQueryableAsync())
                .Where(i => referencedGuids.Contains(i.Id) && i.LessonId != lessonId)
                .ToListAsync();

            foreach (var img in imagesToLink)
            {
                img.LessonId = lessonId;
                if (chapterId.HasValue && chapterId.Value > 0)
                {
                    img.ChapterId = chapterId.Value;
                }
                await _lessonImageRepository.UpdateAsync(img, autoSave: true);
            }
        }

        // 2. Remove orphaned images that belong to this lesson but are no longer referenced in content
        var currentImages = await (await _lessonImageRepository.GetQueryableAsync())
            .Where(i => i.LessonId == lessonId)
            .ToListAsync();

        foreach (var img in currentImages)
        {
            if (!referencedGuids.Contains(img.Id))
            {
                await _storageService.DeleteBlobAsync(img.BlobName);
                await _lessonImageRepository.DeleteAsync(img, autoSave: true);
            }
        }
    }

    public async Task DeleteLessonImagesAsync(long lessonId)
    {
        var images = await (await _lessonImageRepository.GetQueryableAsync())
            .Where(i => i.LessonId == lessonId)
            .ToListAsync();

        foreach (var img in images)
        {
            await _storageService.DeleteBlobAsync(img.BlobName);
            await _lessonImageRepository.DeleteAsync(img, autoSave: true);
        }
    }

    public async Task DeleteSubjectImagesAsync(long subjectId)
    {
        var images = await (await _lessonImageRepository.GetQueryableAsync())
            .Where(i => (i.Lesson != null && i.Lesson.Chapter.SubjectId == subjectId)
                        || (i.Chapter != null && i.Chapter.SubjectId == subjectId))
            .ToListAsync();

        foreach (var img in images)
        {
            await _storageService.DeleteBlobAsync(img.BlobName);
        }
    }

    public async Task<int> CleanupOrphanedImagesAsync(TimeSpan? olderThan = null)
    {
        var cutoff = DateTime.UtcNow - (olderThan ?? TimeSpan.FromHours(24));

        var orphans = await (await _lessonImageRepository.GetQueryableAsync())
            .Where(i => i.LessonId == null && i.CreationTime < cutoff)
            .ToListAsync();

        foreach (var img in orphans)
        {
            await _storageService.DeleteBlobAsync(img.BlobName);
            await _lessonImageRepository.DeleteAsync(img, autoSave: true);
        }

        return orphans.Count;
    }

    private static HashSet<Guid> ExtractImageGuids(string? content)
    {
        var result = new HashSet<Guid>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return result;
        }

        var matches = ImageIdRegex.Matches(content);
        foreach (Match match in matches)
        {
            if (Guid.TryParse(match.Groups[1].Value, out var guid))
            {
                result.Add(guid);
            }
        }

        return result;
    }
}
