namespace LearningBff.Entities;

public enum QuestionType
{
    Single = 1,
    Multi  = 2,
    Essay = 3,
    Semi = 4
}

public enum QuestionDifficulty
{
    Easy   = 1,
    Medium = 2,
    Hard   = 3
}

public enum ExamResultStatus
{
    InProgress = 1,
    Submitted  = 2,
    TimedOut   = 3,
    Cancelled  = 4,
    Expired    = 5
}

public enum ExamGradingStatus
{
    NotRequired = 0, // Dành cho đề 100% trắc nghiệm (máy chấm xong có điểm ngay)
    Pending = 1,     // Chưa chấm (học sinh thấy: "Chờ chấm", ẩn điểm số)
    Grading = 2,     // Đang chấm
    Graded = 3       // Đã chấm xong (học sinh xem được điểm, nhận xét và thi lại)
}

public enum CourseFilter
{
    All,
    Completed,
    Incomplete
}

public enum CourseEnrollmentFilter
{
    All,
    Enrolled,
    NotEnrolled
}
