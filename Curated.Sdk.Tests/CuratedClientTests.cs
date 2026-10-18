using System;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class CuratedClientTests : IDisposable
{
    public CuratedClientTests()
    {
        CuratedClient.Shutdown();
    }

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public void Init_rejects_non_positive_app_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CuratedClient.Init(0, "token"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CuratedClient.Init(-1, "token"));
    }

    [Fact]
    public void Init_rejects_empty_session_token()
    {
        Assert.Throws<ArgumentException>(() => CuratedClient.Init(1, "  "));
    }

    [Fact]
    public void Init_sets_IsValid_and_AppId()
    {
        CuratedClient.InitForTests(42, new RecordingTransport());
        Assert.True(CuratedClient.IsValid);
        Assert.Equal(42, CuratedClient.AppId);
    }

    [Fact]
    public void Init_twice_throws()
    {
        CuratedClient.Init(1, "session-token");
        Assert.Throws<InvalidOperationException>(() =>
            CuratedClient.Init(2, "session-token"));
    }

    [Fact]
    public void Members_before_Init_throw()
    {
        Assert.Throws<InvalidOperationException>(() => _ = CuratedClient.AppId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Trigger_before_Init_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Achievements.TriggerAsync("x"));
    }

    [Fact]
    public void Shutdown_clears_IsValid()
    {
        CuratedClient.InitForTests(1, new RecordingTransport());
        CuratedClient.Shutdown();
        Assert.False(CuratedClient.IsValid);
    }

    [Fact]
    public void Public_surface_hides_transport_types()
    {
        var asm = typeof(CuratedClient).Assembly;
        var publicNames = Array.ConvertAll(
            asm.GetExportedTypes(),
            t => t.Name);
        Array.Sort(publicNames);

        Assert.Contains("CuratedClient", publicNames);
        Assert.Contains("Achievement", publicNames);
        Assert.Contains("Achievements", publicNames);
        Assert.Contains("Cloud", publicNames);
        Assert.Contains("Friends", publicNames);
        Assert.Contains("Presence", publicNames);
        Assert.Contains("CuratedException", publicNames);

        Assert.DoesNotContain("HttpClientTransport", publicNames);
        Assert.DoesNotContain("HttpTransport", publicNames);
        Assert.DoesNotContain("ICuratedTransport", publicNames);
        Assert.DoesNotContain("ITransport", publicNames);
        Assert.DoesNotContain("IBlobTransfer", publicNames);
        Assert.DoesNotContain("HttpBlobTransfer", publicNames);
        Assert.DoesNotContain("RecordingTransport", publicNames);
        Assert.DoesNotContain("AchievementsClient", publicNames);
        Assert.DoesNotContain("CloudSaveClient", publicNames);
        Assert.DoesNotContain("CuratedApiException", publicNames);
        Assert.DoesNotContain("CloudSaveSyncRequest", publicNames);
        Assert.DoesNotContain("CloudSaveSignedUrl", publicNames);

        foreach (var t in asm.GetExportedTypes())
        {
            Assert.False(
                t.Namespace is not null
                    && t.Namespace.Contains(".Internal", StringComparison.Ordinal),
                $"public type leaked from Internal: {t.FullName}");
        }
    }
}
