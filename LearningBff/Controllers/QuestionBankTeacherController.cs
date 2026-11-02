using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;
using LearningBff.Services.Teacher;
using LearningBff.Dtos;

namespace LearningBff.Controllers;

[ApiController]
[Route("api/app/question-bank-teacher")]
[Authorize(Roles = LearningBffConsts.Teacher)]
public class QuestionBankTeacherController : AbpController
{
    private readonly QuestionBankTeacherAppService _questionBankTeacherAppService;

    public QuestionBankTeacherController(QuestionBankTeacherAppService questionBankTeacherAppService)
    {
        _questionBankTeacherAppService = questionBankTeacherAppService;
    }

    [HttpPost("import-question")]
    public async Task ImportQuestion(
        [FromForm] ImportQuestionDto importQuestionDto,
        CancellationToken cancellationToken = default)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers["Cache-Control"] = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";
        await Response.StartAsync(cancellationToken);

        var latestProgress = new ImportQuestionProgressDto(
            "processing",
            0,
            0,
            0,
            0,
            0);

        try
        {
            await WriteSseEventAsync("started", new { message = "Đã tải tệp lên, bắt đầu đọc dữ liệu..." }, cancellationToken);

            var result = await _questionBankTeacherAppService.ImportQuestion(
                importQuestionDto,
                async progress =>
                {
                    latestProgress = progress;
                    await WriteSseEventAsync("progress", progress, cancellationToken);
                },
                cancellationToken);

            await WriteSseEventAsync("complete", result, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The browser disconnected or the user cancelled the import.
        }
        catch (Exception ex)
        {
            await WriteSseEventAsync("error", new
            {
                message = ex.Message,
                importedQuestions = latestProgress.ImportedQuestions,
                errorCount = latestProgress.ErrorCount
            }, cancellationToken);
        }
    }

    private async Task WriteSseEventAsync(string eventName, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        await Response.WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
