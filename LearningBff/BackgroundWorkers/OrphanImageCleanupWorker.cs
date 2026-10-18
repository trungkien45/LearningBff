using System;
using System.Threading.Tasks;
using LearningBff.Services.LessonImages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;

namespace LearningBff.BackgroundWorkers;

/// <summary>
/// Xóa ảnh bài học đã upload nhưng chưa được liên kết với bài học nào sau 24 giờ.
/// Worker chạy mỗi 24 giờ.
/// </summary>
public class OrphanImageCleanupWorker : AsyncPeriodicBackgroundWorkerBase
{
    // Chạy mỗi 24 giờ
    private static readonly TimeSpan WorkerPeriod = TimeSpan.FromHours(24);

    // Xóa ảnh orphan cũ hơn 24 giờ
    private static readonly TimeSpan OlderThan = TimeSpan.FromHours(24);

    public OrphanImageCleanupWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = (int)WorkerPeriod.TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("OrphanImageCleanupWorker: Bắt đầu dọn dẹp ảnh bài học chưa được liên kết...");

        var appService = workerContext.ServiceProvider.GetRequiredService<ILessonImageAppService>();

        try
        {
            var deleted = await appService.CleanupOrphanedImagesAsync(OlderThan);
            Logger.LogInformation("OrphanImageCleanupWorker: Đã xóa {Count} ảnh orphan.", deleted);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "OrphanImageCleanupWorker: Lỗi khi dọn dẹp ảnh orphan.");
        }
    }
}
