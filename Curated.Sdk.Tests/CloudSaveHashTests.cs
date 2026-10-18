using System.Text;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class CloudSaveHashTests
{
    [Fact]
    public void OfBytes_is_stable_sha256_prefix()
    {
        var hash = CloudSaveHash.OfBytes(Encoding.UTF8.GetBytes("hello"));
        Assert.StartsWith("sha256:", hash);
        Assert.Equal(71, hash.Length);
        Assert.Equal(hash, CloudSaveHash.OfBytes(Encoding.UTF8.GetBytes("hello")));
    }

    [Fact]
    public async Task OfFileAsync_matches_bytes()
    {
        var path = Path.Combine(Path.GetTempPath(), "curated-hash-" + Path.GetRandomFileName());
        var bytes = Encoding.UTF8.GetBytes("file-bytes");
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            Assert.Equal(CloudSaveHash.OfBytes(bytes), await CloudSaveHash.OfFileAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
