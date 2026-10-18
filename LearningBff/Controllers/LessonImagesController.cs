using System;
using System.Threading.Tasks;
using LearningBff.Services.LessonImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace LearningBff.Controllers;

[Route("api/lesson-images")]
public class LessonImagesController : AbpControllerBase
{
    private readonly ILessonImageAppService _lessonImageAppService;

    public LessonImagesController(ILessonImageAppService lessonImageAppService)
    {
        _lessonImageAppService = lessonImageAppService;
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromForm] long? chapterId,
        [FromForm] long? lessonId)
    {
        var result = await _lessonImageAppService.UploadAsync(file, chapterId, lessonId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAsync(Guid id)
    {
        var (stream, contentType, fileName) = await _lessonImageAppService.GetFileAsync(id);
        Response.Headers.Append("Cache-Control", "private, max-age=86400");
        return File(stream, contentType, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _lessonImageAppService.DeleteAsync(id);
        return NoContent();
    }
}
