using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LearningBff.Dtos;

public class ImportQuestionDto
{
    [Required]
    public long SubjectId { get; set; }

    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Loại câu hỏi import: 1 = Trắc nghiệm (Single/Multi), 3 = Tự luận (Text), 4 = Bán trắc nghiệm (Mixed)
    /// </summary>
    public int QuestionType { get; set; } = 1;
}
