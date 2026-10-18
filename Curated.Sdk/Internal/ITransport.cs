using System.Threading;
using System.Threading.Tasks;

namespace Curated.Sdk.Internal;

internal interface ITransport
{
    Task<string> SendAsync(
        string method,
        string relativePath,
        object? jsonBody,
        CancellationToken cancellationToken = default);
}
