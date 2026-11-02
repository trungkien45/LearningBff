using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.RazorPages;
using Volo.Abp.Users;
using LearningBff.Services;
using LearningBff.Dtos;

namespace LearningBff.Pages.Exam;

public class TakeModel : AbpPageModel
{
    private readonly ExamAppService _examAppService;
    private readonly ICurrentUser _currentUser;

    public TakeModel(ExamAppService examAppService, ICurrentUser currentUser)
    {
        _examAppService = examAppService;
        _currentUser = currentUser;
    }

    public ExamTakeDto ExamData { get; set; } = null!;
    public bool IsAuthenticated => _currentUser.IsAuthenticated;

    public async Task<IActionResult> OnGetAsync(long resultId)
    {
        if (!IsAuthenticated)
        {
            return Redirect("/Account/Login");
        }

        try
        {
            ExamData = await _examAppService.GetForTakeAsync(resultId);
            return Page();
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage("/Exam/Index");
        }
    }

    public async Task<IActionResult> OnPostSubmitAsync(long examResultId, string? answersJson)
    {
        if (!IsAuthenticated)
        {
            return Redirect("/Account/Login");
        }

        try
        {
            var submitDto = new SubmitExamDto
            {
                ExamResultId = examResultId,
                Answers = new List<StudentAnswerDto>()
            };

            if (!string.IsNullOrWhiteSpace(answersJson))
            {
                using var doc = JsonDocument.Parse(answersJson);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (long.TryParse(prop.Name, out var qId))
                    {
                        var studentAns = new StudentAnswerDto { QuestionId = qId };

                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            studentAns.SelectedAnswerIds = prop.Value.EnumerateArray()
                                .Select(x => x.GetInt64())
                                .ToList();
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("selectedIds", out var idsElem) && idsElem.ValueKind == JsonValueKind.Array)
                            {
                                studentAns.SelectedAnswerIds = idsElem.EnumerateArray()
                                    .Select(x => x.GetInt64())
                                    .ToList();
                            }
                            if (prop.Value.TryGetProperty("text", out var textElem) && textElem.ValueKind == JsonValueKind.String)
                            {
                                studentAns.StudentAnswerText = textElem.GetString();
                            }
                        }

                        submitDto.Answers.Add(studentAns);
                    }
                }
            }

            var result = await _examAppService.SubmitExamAsync(submitDto);
            return RedirectToPage("/Exam/Result", new { resultId = result.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToPage("/Exam/Index");
        }
    }
}
