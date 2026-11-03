using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

[Collection(OverlayEnvCollection.Name)]
public class EditorLauncherBridgeTests : IDisposable
{
    private readonly string? _savedBridge = Environment.GetEnvironmentVariable(EditorLauncherBridge.BridgeVariable);
    private readonly string? _savedOverlay = Environment.GetEnvironmentVariable(LaunchContext.OverlayEndpointVariable);
    private readonly string? _savedToken = Environment.GetEnvironmentVariable(LaunchContext.OverlayTokenVariable);

    public EditorLauncherBridgeTests()
    {
        Environment.SetEnvironmentVariable(EditorLauncherBridge.BridgeVariable, "127.0.0.1:9");
        OverlayChannel.Clear();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EditorLauncherBridge.BridgeVariable, _savedBridge);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, _savedOverlay);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, _savedToken);
        OverlayChannel.Clear();
    }

    [Fact]
    public void BuildAttachRequest_sends_the_signed_in_account_and_no_launch_flag()
    {
        var json = EditorLauncherBridge.BuildAttachRequest(1002, "a\"b\\c");

        Assert.Equal(
            "{\"v\":1,\"type\":\"editor_attach\",\"app_id\":1002,\"token\":\"a\\\"b\\\\c\"}",
            json);
        Assert.DoesNotContain("CURATED_LAUNCHER", json);
    }

    [Fact]
    public void ParseAttachResponse_adopts_endpoint_token_and_overlay_url()
    {
        var result = EditorLauncherBridge.ParseAttachResponse(
            """{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":"ov","overlay_url":"http://127.0.0.1:4060/overlay.html?editor=1#ov"}""");

        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.False(result.NotSignedIn);
        Assert.Equal("127.0.0.1:4050", result.Endpoint);
        Assert.Equal("ov", result.OverlayToken);
        Assert.Equal("http://127.0.0.1:4060/overlay.html?editor=1#ov", result.OverlayUrl);
    }

    [Theory]
    [InlineData("""{"v":1,"type":"editor_attach_result","error":"not_signed_in"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","ok":false,"error":"not_signed_in"}""")]
    public void ParseAttachResponse_reports_not_signed_in(string line)
    {
        var result = EditorLauncherBridge.ParseAttachResponse(line);

        Assert.NotNull(result);
        Assert.True(result!.NotSignedIn);
        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("""{"v":1,"type":"editor_attach_result","endpoint":"8.8.8.8:4050","overlay_token":"ov","overlay_url":"http://127.0.0.1:4050/overlay.html?editor=1#ov"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":"ov","overlay_url":"http://8.8.8.8:4050/overlay.html?editor=1#ov"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":"ov","overlay_url":"file:///tmp/overlay.html#ov"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":"ov","overlay_url":"http://127.0.0.1:4050/overlay.html?editor=1#other"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","ok":true,"overlay_endpoint":"127.0.0.1:4050","overlay_token":"ov","session_token":"sess"}""")]
    [InlineData("""{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":" ","overlay_url":"http://127.0.0.1:4050/overlay.html?editor=1#ov"}""")]
    [InlineData("not-json")]
    public void ParseAttachResponse_rejects_a_reply_the_editor_must_not_use(string line)
    {
        Assert.Null(EditorLauncherBridge.ParseAttachResponse(line));
    }

    [Fact]
    public void ReadBridgeEndpoint_prefers_editor_endpoint()
    {
        Assert.Equal(
            "127.0.0.1:47921",
            EditorLauncherBridge.ReadBridgeEndpoint("""{"v":1,"endpoint":"127.0.0.1:47921"}"""));
        Assert.Equal(
            "127.0.0.1:47922",
            EditorLauncherBridge.ReadBridgeEndpoint("""{"editor_endpoint":"127.0.0.1:47922","endpoint":"127.0.0.1:1"}"""));
        Assert.Null(EditorLauncherBridge.ReadBridgeEndpoint("""{"v":1}"""));
    }

    [Fact]
    public async Task TryAttachAsync_sends_the_account_token_and_adopts_the_reply()
    {
        Environment.SetEnvironmentVariable(LaunchContext.LauncherVariable, null);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Environment.SetEnvironmentVariable(EditorLauncherBridge.BridgeVariable, $"127.0.0.1:{port}");
        var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[512];
            var text = new StringBuilder();
            while (text.ToString().IndexOf('\n') < 0)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    return;
                }

                text.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }

            seen.TrySetResult(text.ToString().Split('\n')[0]);
            var body = """{"v":1,"type":"editor_attach_result","endpoint":"127.0.0.1:4050","overlay_token":"overlay-secret","overlay_url":"http://127.0.0.1:4060/overlay.html?editor=1#overlay-secret"}""";
            var bytes = Encoding.UTF8.GetBytes(body + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
        });

        var result = await EditorLauncherBridge.TryAttachAsync(1002, "account-token", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.Equal("account-token", result.AccountToken);
        Assert.Equal("127.0.0.1:4050", result.Endpoint);
        Assert.Equal(
            "http://127.0.0.1:4060/overlay.html?editor=1#overlay-secret",
            result.OverlayUrl);
        Assert.Equal(
            "{\"v\":1,\"type\":\"editor_attach\",\"app_id\":1002,\"token\":\"account-token\"}",
            await seen.Task);
        EditorLauncherBridge.Apply(result);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        Assert.True(OverlayChannel.TryResolve(out var endpoint, out var token));
        Assert.Equal("127.0.0.1:4050", endpoint);
        Assert.Equal("overlay-secret", token);
        Assert.Equal(result.OverlayUrl, OverlayChannel.OverlayUrl);
        listener.Stop();
        await serve;
    }

    [Fact]
    public async Task TryAttachAsync_not_signed_in_is_not_success()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Environment.SetEnvironmentVariable(EditorLauncherBridge.BridgeVariable, $"127.0.0.1:{port}");
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[256];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    return;
                }

                if (Array.IndexOf(buffer, (byte)'\n', 0, read) >= 0)
                {
                    break;
                }
            }

            var body = """{"v":1,"type":"editor_attach_result","error":"not_signed_in"}""";
            var bytes = Encoding.UTF8.GetBytes(body + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
        });

        var result = await EditorLauncherBridge.TryAttachAsync(1002, "account-token", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.NotSignedIn);
        Assert.False(result.Succeeded);
        EditorLauncherBridge.Apply(result);
        Assert.False(OverlayChannel.TryResolve(out _, out _));
        listener.Stop();
        await serve;
    }

    [Fact]
    public async Task TryAttachAsync_without_a_listener_or_account_returns_null()
    {
        Environment.SetEnvironmentVariable(EditorLauncherBridge.BridgeVariable, "127.0.0.1:9");

        Assert.Null(await EditorLauncherBridge.TryAttachAsync(1002, "account-token", CancellationToken.None));
        Assert.Null(await EditorLauncherBridge.TryAttachAsync(0, "account-token", CancellationToken.None));
        Assert.Null(await EditorLauncherBridge.TryAttachAsync(1002, " ", CancellationToken.None));
    }

    [Fact]
    public void Adopted_credentials_win_over_process_env()
    {
        OverlayChannel.Adopt(
            "127.0.0.1:4050",
            "adopted",
            "http://127.0.0.1:4050/overlay.html?editor=1#adopted");
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, "127.0.0.1:9");
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, "from-env");

        Assert.True(OverlayChannel.TryResolve(out var endpoint, out var token));
        Assert.Equal("127.0.0.1:4050", endpoint);
        Assert.Equal("adopted", token);

        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        Assert.True(OverlayChannel.TryResolve(out endpoint, out token));
        Assert.Equal("adopted", token);

        OverlayChannel.Clear();
        Assert.False(OverlayChannel.TryResolve(out _, out _));
    }

    [Fact]
    public async Task Achievement_notify_uses_the_adopted_overlay_when_env_is_empty()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        OverlayChannel.Adopt(
            $"127.0.0.1:{port}",
            "adopted-token",
            $"http://127.0.0.1:{port}/overlay.html?editor=1#adopted-token");
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        var seen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[1024];
            var text = new StringBuilder();
            while (text.ToString().IndexOf('\n') < 0)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    return;
                }

                text.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }

            seen.TrySetResult(text.ToString());
        });

        OverlayNotifier.NotifyAchievementUnlocked(1002, "as_it_comes", "As it Comes", "Complete mission 7", null, 4.9);
        var completed = await Task.WhenAny(seen.Task, Task.Delay(2000));

        Assert.Same(seen.Task, completed);
        var line = await seen.Task;
        Assert.Contains("\"token\":\"adopted-token\"", line);
        Assert.Contains("\"type\":\"achievement_unlocked\"", line);
        listener.Stop();
        await serve;
    }
}
