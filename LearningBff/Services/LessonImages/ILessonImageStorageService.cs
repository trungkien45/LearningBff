using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Volo.Abp.DependencyInjection;

namespace LearningBff.Services.LessonImages;

public interface ILessonImageStorageService : ITransientDependency
{
    Task<string> SaveBlobAsync(Stream stream, string extension, CancellationToken cancellationToken = default);
    Task<Stream?> GetBlobStreamAsync(string blobName, CancellationToken cancellationToken = default);
    Task DeleteBlobAsync(string blobName, CancellationToken cancellationToken = default);
}
