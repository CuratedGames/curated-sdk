using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class OpenIdIdTokenTests : IDisposable
{
    public OpenIdIdTokenTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task GetOpenIdIdTokenAsync_posts_app_id_and_returns_id_token()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(1002, transport);

        var token = await CuratedClient.GetOpenIdIdTokenAsync();

        Assert.Equal("header.payload.signature", token);
        Assert.Single(transport.Calls);
        Assert.Equal("POST", transport.Calls[0].Method);
        Assert.Equal("/auth/openid/id-token", transport.Calls[0].RelativePath);
        var body = Assert.IsType<OpenIdIdTokenRequest>(transport.Calls[0].Body);
        Assert.Equal(1002, body.AppId);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_empty_id_token_is_hub_error()
    {
        var transport = new RecordingTransport((_, _, _) =>
            """{"id_token":"","expires_in":600}""");
        CuratedClient.InitForTests(1002, transport);

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("hub_error", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_missing_id_token_is_hub_error()
    {
        var transport = new RecordingTransport((_, _, _) => """{"expires_in":600}""");
        CuratedClient.InitForTests(1002, transport);

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("hub_error", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_whitespace_id_token_is_hub_error()
    {
        var transport = new RecordingTransport((_, _, _) =>
            """{"id_token":"   ","expires_in":600}""");
        CuratedClient.InitForTests(1002, transport);

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("hub_error", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_http_401_is_session_invalid()
    {
        CuratedClient.InitForTests(
            1002,
            new ThrowingTransport(new CuratedException(HttpStatusCode.Unauthorized, null, "unauthorized")));

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("session_invalid", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_http_403_is_hub_error()
    {
        CuratedClient.InitForTests(
            1002,
            new ThrowingTransport(new CuratedException(HttpStatusCode.Forbidden, null, "forbidden")));

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("hub_error", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_network_failure_is_network_error()
    {
        CuratedClient.InitForTests(
            1002,
            new ThrowingTransport(new HttpRequestException("offline")));

        var e = await Assert.ThrowsAsync<CuratedLaunchException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal("network_error", e.Code);
    }

    [Fact]
    public async Task GetOpenIdIdTokenAsync_before_Init_throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CuratedClient.GetOpenIdIdTokenAsync());
    }

    private sealed class ThrowingTransport : ITransport
    {
        private readonly Exception _exception;

        public ThrowingTransport(Exception exception) => _exception = exception;

        public Task<string> SendAsync(
            string method,
            string relativePath,
            object? jsonBody,
            CancellationToken cancellationToken = default) =>
            Task.FromException<string>(_exception);
    }
}
