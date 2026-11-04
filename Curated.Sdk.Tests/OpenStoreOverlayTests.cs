using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

[Collection(OverlayEnvCollection.Name)]
public class OpenStoreOverlayTests : IDisposable
{
    private const string UnpublishedCartMessage =
        "Add to cart is only available for released or preorderable apps. This store page is published but not released. Use OverlayToStoreFlag.None.";

    public OpenStoreOverlayTests()
    {
        CuratedClient.Shutdown();
        ClearOverlay();
    }

    public void Dispose()
    {
        ClearOverlay();
        OverlayStore.ConnectTimeout = TimeSpan.FromSeconds(2);
        OverlayStore.ReadTimeout = TimeSpan.FromSeconds(15);
        CuratedClient.Shutdown();
    }

    [Theory]
    [InlineData(OverlayToStoreFlag.None, 0)]
    [InlineData(OverlayToStoreFlag.AddToCart, 1)]
    [InlineData(OverlayToStoreFlag.AddToCartAndShow, 2)]
    public void BuildRequest_is_one_overlay_line(OverlayToStoreFlag flag, int wireFlag)
    {
        var json = OverlayStore.BuildRequest("tok\"en", 1002, flag);

        Assert.Equal(
            "{\"v\":1,\"token\":\"tok\\\"en\",\"type\":\"open_store_overlay\",\"app_id\":1002,\"flag\":" + wireFlag + "}",
            json);
    }

    [Fact]
    public void OpenStoreOverlay_asks_the_overlay_for_the_requested_app()
    {
        string? seen = null;
        using var server = StartOverlay(line =>
        {
            seen = line;
            return "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":true}";
        });
        var transport = new RecordingTransport((_, _, _) => throw new InvalidOperationException("client must not POST"));
        CuratedClient.InitForTests(1002, transport);

        Friends.OpenStoreOverlay(4400, OverlayToStoreFlag.AddToCartAndShow);

        Assert.Empty(transport.Calls);
        Assert.Equal(
            "{\"v\":1,\"token\":\"overlay-token\",\"type\":\"open_store_overlay\",\"app_id\":4400,\"flag\":2}",
            seen);
    }

    [Theory]
    [InlineData(OverlayToStoreFlag.None)]
    [InlineData(OverlayToStoreFlag.AddToCart)]
    [InlineData(OverlayToStoreFlag.AddToCartAndShow)]
    public void OpenStoreOverlay_accepts_each_flag_when_the_launcher_accepts_it(OverlayToStoreFlag flag)
    {
        using var server = StartOverlay(_ =>
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":true}");
        CuratedClient.InitForTests(1002, new RecordingTransport());

        Friends.OpenStoreOverlay(1002, flag);
    }

    [Fact]
    public void OpenStoreOverlay_passes_through_the_launcher_message()
    {
        using var server = StartOverlay(_ =>
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":false,\"error\":\"" + UnpublishedCartMessage + "\"}");
        CuratedClient.InitForTests(1002, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(1002, OverlayToStoreFlag.AddToCart));

        Assert.Equal(UnpublishedCartMessage, ex.Message);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("\"ok\":false", ex.ResponseBody);
    }

    [Theory]
    [InlineData("The Curated overlay is turned off in launcher settings.")]
    [InlineData("The Curated overlay is not active for this application.")]
    [InlineData("This app does not have a published Curated store page.")]
    public void OpenStoreOverlay_surfaces_other_launcher_messages(string message)
    {
        using var server = StartOverlay(_ =>
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":false,\"error\":\"" + message + "\"}");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(8, OverlayToStoreFlag.None));

        Assert.Equal(message, ex.Message);
    }

    [Fact]
    public void OpenStoreOverlay_decodes_the_launcher_message()
    {
        using var server = StartOverlay(_ =>
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":false,\"error\":\"Say \\\"hi\\\"\"}");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(8, OverlayToStoreFlag.AddToCartAndShow));

        Assert.Equal("Say \"hi\"", ex.Message);
    }

    [Fact]
    public void OpenStoreOverlay_missing_overlay_is_an_error()
    {
        var transport = new RecordingTransport((_, _, _) => throw new InvalidOperationException("client must not POST"));
        CuratedClient.InitForTests(3, transport);

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(9, OverlayToStoreFlag.None));

        Assert.Empty(transport.Calls);
        Assert.Equal(OverlayStore.OverlayUnavailableMessage, ex.Message);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        Assert.Null(ex.ResponseBody);
    }

    [Fact]
    public void OpenStoreOverlay_unreadable_overlay_line_is_an_error()
    {
        using var server = StartOverlay(_ => "not-json");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(4, OverlayToStoreFlag.None));

        Assert.Equal(OverlayStore.UnreadableResultMessage, ex.Message);
    }

    [Fact]
    public void OpenStoreOverlay_wrong_result_type_is_an_error()
    {
        using var server = StartOverlay(_ =>
            "{\"v\":1,\"type\":\"microtxn_purchase_result\",\"ok\":true}");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(4, OverlayToStoreFlag.None));

        Assert.Equal(OverlayStore.UnreadableResultMessage, ex.Message);
    }

    [Fact]
    public void OpenStoreOverlay_overlay_timeout_is_an_error()
    {
        OverlayStore.ReadTimeout = TimeSpan.FromMilliseconds(200);
        using var server = StartOverlay(_ => null);
        CuratedClient.InitForTests(3, new RecordingTransport());

        var ex = Assert.Throws<CuratedException>(() =>
            Friends.OpenStoreOverlay(1, OverlayToStoreFlag.None));

        Assert.Equal(OverlayStore.OverlaySilentMessage, ex.Message);
    }

    [Fact]
    public void OpenStoreOverlay_uses_the_adopted_overlay_when_env_is_empty()
    {
        string? seen = null;
        using var server = StartOverlay(line =>
        {
            seen = line;
            return "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":true}";
        });
        var endpoint = Environment.GetEnvironmentVariable(LaunchContext.OverlayEndpointVariable);
        Assert.False(string.IsNullOrEmpty(endpoint));
        OverlayChannel.Adopt(endpoint!, "channel-token", "http://127.0.0.1:1/overlay.html?editor=1#channel-token");
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        CuratedClient.InitForTests(3, new RecordingTransport());

        Friends.OpenStoreOverlay(77, OverlayToStoreFlag.None);

        Assert.Contains("\"token\":\"channel-token\"", seen);
        Assert.Contains("\"app_id\":77", seen);
        Assert.DoesNotContain("overlay-token", seen);
    }

    [Fact]
    public void OpenStoreOverlay_requires_init_and_a_positive_app_id()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Friends.OpenStoreOverlay(1, OverlayToStoreFlag.None));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Friends.OpenStoreOverlay(0, OverlayToStoreFlag.AddToCart));

        CuratedClient.InitForTestsWithoutEntitlement(1002, new RecordingTransport());
        var ex = Assert.Throws<CuratedLaunchException>(() =>
            Friends.OpenStoreOverlay(1002, OverlayToStoreFlag.None));
        Assert.Equal(CuratedLaunchError.NotEntitled, ex.Error);
    }

    [Fact]
    public void TryReadResult_requires_ok()
    {
        Assert.True(OverlayStore.TryReadResult(
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":true}",
            out var ok,
            out var error));
        Assert.True(ok);
        Assert.Null(error);

        Assert.True(OverlayStore.TryReadResult(
            "{\"v\":1,\"type\":\"open_store_overlay_result\",\"ok\":false,\"error\":\"no\"}",
            out ok,
            out error));
        Assert.False(ok);
        Assert.Equal("no", error);

        Assert.False(OverlayStore.TryReadResult(
            "{\"v\":1,\"type\":\"open_store_overlay_result\"}",
            out _,
            out _));
    }

    private static OverlayServer StartOverlay(Func<string, string?> reply)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, $"127.0.0.1:{port}");
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, "overlay-token");
        var cancel = new CancellationTokenSource();
        var loop = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[4096];
            var text = new StringBuilder();
            while (text.ToString().IndexOf('\n') < 0)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancel.Token);
                if (read == 0)
                {
                    return;
                }

                text.Append(Encoding.UTF8.GetString(buffer, 0, read));
            }

            var line = text.ToString().Split('\n')[0];
            var response = reply(line);
            if (response is null)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancel.Token);
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(response + "\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, cancel.Token);
        });
        return new OverlayServer(listener, loop, cancel);
    }

    private static void ClearOverlay()
    {
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        OverlayChannel.Clear();
    }

    private sealed class OverlayServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly Task _loop;
        private readonly CancellationTokenSource _cancel;

        public OverlayServer(TcpListener listener, Task loop, CancellationTokenSource cancel)
        {
            _listener = listener;
            _loop = loop;
            _cancel = cancel;
        }

        public void Dispose()
        {
            _cancel.Cancel();
            _listener.Stop();
            try
            {
                _loop.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException)
            {
            }

            _cancel.Dispose();
        }
    }
}
