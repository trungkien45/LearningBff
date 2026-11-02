using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Identity;

namespace LearningBff.Entities;

public class ExamResult : FullAuditedAggregateRoot<long>
{
    public long ExamId { get; set; }
    public string? ExamTitle { get; set; }
    public Exam? Exam { get; set; }

    public List<ExamResultAnswer> ExamResultAnswers { get; set; } = [];

    /// <summary>
    /// Student (User) ID in ABP Identity (AbpUsers.Id)
    /// </summary>
    public Guid UserId { get; set; }
    public IdentityUser? User { get; set; }

    /// <summary>
    /// Alias helper for StudentId (links to UserId)
    /// </summary>
    [NotMapped]
    public Guid StudentId
    {
        get => UserId;
        set => UserId = value;
    }

    public float Score { get; set; }
    public float MaxScore { get; set; } = 10.0f;
    public bool IsPassed { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? SubmitTime { get; set; }

    public ExamResultStatus Status { get; set; } = ExamResultStatus.InProgress;

    /// <summary>
    /// Trạng thái chấm bài của giáo viên.
    /// NotRequired: Đề 100% trắc nghiệm (máy chấm ngay).
    /// Pending: Đề có tự luận/bán trắc nghiệm đang chờ giáo viên chấm (học sinh thấy: "Chưa chấm").
    /// Graded: Đã chấm xong (học sinh xem được điểm và thi lại nếu được phép).
    /// </summary>
    public ExamGradingStatus GradingStatus { get; set; } = ExamGradingStatus.NotRequired;

    /// <summary>
    /// Giáo viên chấm bài
    /// </summary>
    public Guid? GradedByUserId { get; set; }
    public IdentityUser? GradedByUser { get; set; }

    /// <summary>
    /// Thời điểm hoàn tất chấm bài
    /// </summary>
    public DateTime? GradedTime { get; set; }

    /// <summary>
    /// Nhận xét tổng quan của giáo viên cho cả bài thi
    /// </summary>
    public string? OverallFeedback { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public ExamResult()
    {
    }

    public ExamResult(long examId, Guid userId, int attemptNumber = 1)
    {
        ExamId = examId;
        UserId = userId;
        AttemptNumber = attemptNumber;
        StartTime = DateTime.UtcNow;
        Status = ExamResultStatus.InProgress;
    }
}
