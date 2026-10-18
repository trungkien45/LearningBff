using System;
using System.IO;
using System.Threading.Tasks;
using LearningBff.Dtos;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Application.Services;

namespace LearningBff.Services.LessonImages;

public interface ILessonImageAppService : IApplicationService
{
    Task<LessonImageUploadResultDto> UploadAsync(IFormFile file, long? chapterId, long? lessonId);
    Task<(Stream Stream, string ContentType, string FileName)> GetFileAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task SyncLessonImagesAsync(long lessonId, string? content, long? chapterId = null);
    Task DeleteLessonImagesAsync(long lessonId);
    Task DeleteSubjectImagesAsync(long subjectId);
    Task<int> CleanupOrphanedImagesAsync(TimeSpan? olderThan = null);
}
