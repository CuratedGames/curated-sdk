using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

[CollectionDefinition(OverlayEnvCollection.Name, DisableParallelization = true)]
public sealed class OverlayEnvCollection
{
    public const string Name = "CuratedOverlayEnv";
}

[Collection(OverlayEnvCollection.Name)]
public class MicroTxnTests : IDisposable
{
    public MicroTxnTests()
    {
        CuratedClient.Shutdown();
        ClearOverlay();
    }

    public void Dispose()
    {
        ClearOverlay();
        OverlayPurchase.ConnectTimeout = TimeSpan.FromSeconds(2);
        OverlayPurchase.ReadTimeout = TimeSpan.FromMinutes(10);
        CuratedClient.Shutdown();
    }

    [Fact]
    public void BuildRequest_is_one_overlay_line_without_the_player_boolean()
    {
        var json = OverlayPurchase.BuildRequest("tok\"en", 1002, 55, 77);

        Assert.Equal(
            "{\"v\":1,\"token\":\"tok\\\"en\",\"type\":\"microtxn_purchase\",\"app_id\":1002,\"order_id\":55,\"trans_id\":77}",
            json);
        Assert.DoesNotContain("authorized", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthorizeAsync_asks_the_overlay_and_ignores_the_game_boolean()
    {
        string? seen = null;
        using var server = await StartOverlay(line =>
        {
            seen = line;
            return """{"v":1,"type":"microtxn_purchase_result","authorized":false,"status":"Denied","order_id":55,"trans_id":77,"error_code":null,"error_description":null}""";
        });
        var transport = new RecordingTransport((_, _, _) => throw new InvalidOperationException("client must not POST"));
        CuratedClient.InitForTests(1002, transport);

        var result = await MicroTxn.AuthorizeAsync(55, 77, authorized: true);

        Assert.Empty(transport.Calls);
        Assert.Equal(
            "{\"v\":1,\"token\":\"overlay-token\",\"type\":\"microtxn_purchase\",\"app_id\":1002,\"order_id\":55,\"trans_id\":77}",
            seen);
        Assert.Equal(MicroTxnAuthorizationStatus.Denied, result.Status);
        Assert.Equal(55, result.OrderId);
        Assert.Equal(77, result.TransId);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorDescription);
    }

    [Fact]
    public async Task AuthorizeAsync_maps_authorized_status()
    {
        using var server = await StartOverlay(_ =>
            """{"v":1,"type":"microtxn_purchase_result","authorized":true,"status":"Authorized","order_id":9,"trans_id":8,"error_code":null,"error_description":null}""");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var result = await MicroTxn.AuthorizeAsync(9, 8, authorized: false);

        Assert.Equal(MicroTxnAuthorizationStatus.Authorized, result.Status);
        Assert.Equal(9, result.OrderId);
        Assert.Equal(8, result.TransId);
    }

    [Fact]
    public async Task AuthorizeAsync_maps_overlay_error()
    {
        using var server = await StartOverlay(_ =>
            """{"v":1,"type":"microtxn_purchase_result","authorized":false,"status":"Error","order_id":11,"trans_id":12,"error_code":7,"error_description":"Order is not pending"}""");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var result = await MicroTxn.AuthorizeAsync(11, 12, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal(7, result.ErrorCode);
        Assert.Equal("Order is not pending", result.ErrorDescription);
        Assert.Equal(11, result.OrderId);
        Assert.Equal(12, result.TransId);
    }

    [Fact]
    public async Task AuthorizeAsync_missing_overlay_is_error_and_does_not_post()
    {
        var transport = new RecordingTransport((_, _, _) => throw new InvalidOperationException("client must not POST"));
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: true);

        Assert.Empty(transport.Calls);
        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal("Purchases require the Curated launcher overlay.", result.ErrorDescription);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task AuthorizeAsync_unreadable_overlay_line_is_error()
    {
        using var server = await StartOverlay(_ => "not-json");
        CuratedClient.InitForTests(3, new RecordingTransport());

        var result = await MicroTxn.AuthorizeAsync(4, 5, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal("Curated overlay returned an unreadable purchase result.", result.ErrorDescription);
    }

    [Fact]
    public async Task AuthorizeAsync_overlay_timeout_is_error()
    {
        OverlayPurchase.ReadTimeout = TimeSpan.FromMilliseconds(200);
        using var server = await StartOverlay(_ => null);
        CuratedClient.InitForTests(3, new RecordingTransport());

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: false);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal("Curated overlay did not answer.", result.ErrorDescription);
    }

    [Fact]
    public async Task AuthorizeAsync_requires_init_and_positive_ids()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MicroTxn.AuthorizeAsync(1, 2, true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MicroTxn.AuthorizeAsync(0, 2, true).GetAwaiter().GetResult());

        CuratedClient.InitForTestsWithoutEntitlement(1002, new RecordingTransport());
        var ex = await Assert.ThrowsAsync<CuratedLaunchException>(() =>
            MicroTxn.AuthorizeAsync(1, 2, true));
        Assert.Equal(CuratedLaunchError.NotEntitled, ex.Error);
    }

    [Fact]
    public void TryParseResult_trusts_status_not_the_authorized_flag()
    {
        var ok = OverlayPurchase.TryParseResult(
            """{"v":1,"type":"microtxn_purchase_result","authorized":false,"status":"Authorized","order_id":"55","trans_id":77,"error_code":null,"error_description":null}""",
            1,
            2,
            out var result);

        Assert.True(ok);
        Assert.Equal(MicroTxnAuthorizationStatus.Authorized, result.Status);
        Assert.Equal(55, result.OrderId);
        Assert.Equal(77, result.TransId);
    }

    private static Task<OverlayServer> StartOverlay(Func<string, string?> reply)
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
        return Task.FromResult(new OverlayServer(listener, loop, cancel));
    }

    private static void ClearOverlay()
    {
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
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
