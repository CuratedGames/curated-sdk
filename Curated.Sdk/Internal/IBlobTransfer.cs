using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal interface IBlobTransfer
{
    Task UploadAsync(
        string signedUrl,
        Stream content,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string signedUrl,
        Stream destination,
        CancellationToken cancellationToken = default);
}
