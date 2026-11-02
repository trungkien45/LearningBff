using Volo.Abp.Domain.Entities;

namespace LearningBff.Entities;

public class ExamResultAnswer : Entity<long>
{
    public long ExamResultId { get; set; }
    public ExamResult? ExamResult { get; set; }

    public long QuestionId { get; set; }
    public Question? Question { get; set; }

    /// <summary>
    /// ID phương án trắc nghiệm được chọn.
    /// Để null nếu là câu hỏi Tự luận thuần túy (Text).
    /// Đối với Bán trắc nghiệm (Mixed), lưu ID đáp án trắc nghiệm mà học sinh chọn.
    /// </summary>
    public long? AnswerId { get; set; }
    public Answer? Answer { get; set; }

    /// <summary>
    /// Bản sao nội dung phương án trắc nghiệm đã chọn tại thời điểm nộp bài
    /// </summary>
    public string AnswerText { get; set; } = string.Empty;

    public bool IsSelected { get; set; } = true;

    /// <summary>
    /// Đánh dấu đúng/sai (cho trắc nghiệm do máy chấm, hoặc giáo viên xác nhận)
    /// </summary>
    public bool IsCorrect { get; set; }

    /// <summary>
    /// Nội dung trả lời bằng văn bản của học sinh (bắt buộc đối với Tự luận và phần text của Bán trắc nghiệm)
    /// </summary>
    public string? StudentAnswerText { get; set; }

    /// <summary>
    /// Điểm số đạt được của câu này
    /// </summary>
    public float EarnedScore { get; set; }

    /// <summary>
    /// Nhận xét, góp ý của giáo viên cho riêng câu trả lời này
    /// </summary>
    public string? TeacherFeedback { get; set; }

    /// <summary>
    /// Đã được giáo viên chấm/review câu này hay chưa
    /// </summary>
    public bool IsGraded { get; set; }

    public ExamResultAnswer()
    {
    }

    public ExamResultAnswer(long examResultId, long questionId, long? answerId = null, bool isCorrect = false, float earnedScore = 0.0f)
    {
        ExamResultId = examResultId;
        QuestionId = questionId;
        AnswerId = answerId;
        IsSelected = true;
        IsCorrect = isCorrect;
        EarnedScore = earnedScore;
    }
}
