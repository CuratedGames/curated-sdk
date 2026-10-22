using System.IO;
using System.Text;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class CloudTests : IDisposable
{
    public CloudTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task SyncOnQuitAsync_hashes_and_uploads()
    {
        var root = Path.Combine(Path.GetTempPath(), "curated-sdk-cs-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(root, "saves"));
        var savePath = Path.Combine(root, "saves", "slot1.sav");
        var payload = Encoding.UTF8.GetBytes("save-v1");
        await File.WriteAllBytesAsync(savePath, payload);

        var transport = new RecordingTransport();
        var blobs = new MemoryBlobTransfer();
        CuratedClient.InitForTests(77, transport, blobs);

        try
        {
            var result = await Cloud.SyncOnQuitAsync(root, "saves/slot1.sav");

            Assert.Contains("saves/slot1.sav", result.TransferredPaths);
            Assert.Single(blobs.Store);
            Assert.Equal(payload, Assert.Single(blobs.Store.Values));
            Assert.Equal("POST", transport.Calls[0].Method);
            Assert.Equal("/games/app/77/cloud-save/sync", transport.Calls[0].RelativePath);
            var body = Assert.IsType<CloudSaveSyncRequest>(transport.Calls[0].Body);
            Assert.Equal("upload", body.Direction);
            Assert.StartsWith("sha256:", body.Files[0].ContentHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task SyncOnLaunchAsync_downloads_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "curated-sdk-cs-" + Path.GetRandomFileName());
        var transport = new RecordingTransport((method, path, body) =>
        {
            if (path.Contains("/cloud-save/sync", System.StringComparison.Ordinal))
            {
                return Json.Serialize(
                    new CloudSaveSyncResultWire
                    {
                        Direction = "download",
                        ConflictPolicy = "last_write_wins",
                        Urls = new[]
                        {
                            new CloudSaveSignedUrl
                            {
                                RelativePath = "saves/slot1.sav",
                                Action = "get",
                                Url = "https://cdn.test/cloud-save/slot1"
                            },
                            new CloudSaveSignedUrl
                            {
                                RelativePath = "saves/skip.sav",
                                Action = "unchanged",
                                Url = null
                            }
                        }
                    });
            }

            return "{}";
        });
        var blobs = new MemoryBlobTransfer();
        blobs.Seed("https://cdn.test/cloud-save/slot1", Encoding.UTF8.GetBytes("remote-bytes"));
        CuratedClient.InitForTests(88, transport, blobs);

        try
        {
            var result = await Cloud.SyncOnLaunchAsync(root);
            var local = Path.Combine(root, "saves", "slot1.sav");
            Assert.True(File.Exists(local));
            Assert.Equal("remote-bytes", await File.ReadAllTextAsync(local));
            Assert.Contains("saves/slot1.sav", result.TransferredPaths);
            Assert.DoesNotContain("saves/skip.sav", result.TransferredPaths);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SyncOnQuitAsync_rejects_path_escape()
    {
        var root = Path.Combine(Path.GetTempPath(), "curated-sdk-cs-" + Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        CuratedClient.InitForTests(1, new RecordingTransport(), new MemoryBlobTransfer());
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                Cloud.SyncOnQuitAsync(root, "../secret.sav"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NeedsTransfer_skips_unchanged()
    {
        Assert.False(CloudSaveService.NeedsTransfer(
            new CloudSaveSignedUrl { Action = "unchanged", Url = "https://x" },
            "put"));
        Assert.True(CloudSaveService.NeedsTransfer(
            new CloudSaveSignedUrl { Action = "put", Url = "https://x" },
            "put"));
        Assert.False(CloudSaveService.NeedsTransfer(
            new CloudSaveSignedUrl { Action = "put", Url = null },
            "put"));
    }
}
