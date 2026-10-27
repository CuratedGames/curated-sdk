using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class EntitlementGateTests : IDisposable
{
    private const long AppId = 1002;

    public EntitlementGateTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task InitAsync_requires_ownership_and_sets_session()
    {
        var session = await CuratedClient.InitAsync(
            AppId,
            "tok",
            CuratedClient.DefaultHubUrl,
            new OwnedHandler(),
            CancellationToken.None);

        Assert.True(CuratedClient.IsValid);
        Assert.True(CuratedClient.IsEntitled);
        Assert.NotNull(CuratedClient.Session);
        Assert.Equal("player", session.Username);
        Assert.True(session.Entitlement.IsEntitled);
    }

    [Fact]
    public async Task InitAsync_not_entitled_fails_closed()
    {
        var ex = await Assert.ThrowsAsync<CuratedLaunchException>(() =>
            CuratedClient.InitAsync(
                AppId,
                "tok",
                CuratedClient.DefaultHubUrl,
                new NotOwnedHandler(),
                CancellationToken.None));

        Assert.Equal(CuratedLaunchError.NotEntitled, ex.Error);
        Assert.Equal("not_entitled", ex.Code);
        Assert.False(CuratedClient.IsValid);
        Assert.False(CuratedClient.IsEntitled);
        Assert.Null(CuratedClient.Session);
    }

    [Fact]
    public void Init_sync_not_entitled_fails_closed()
    {
        var ex = Assert.Throws<CuratedLaunchException>(() =>
            CuratedClient.InitAsync(
                    AppId,
                    "tok",
                    CuratedClient.DefaultHubUrl,
                    new NotOwnedHandler(),
                    CancellationToken.None)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult());

        Assert.Equal("not_entitled", ex.Code);
        Assert.False(CuratedClient.IsValid);
    }

    [Fact]
    public async Task Game_apis_fail_closed_without_entitlement_verification()
    {
        CuratedClient.InitForTestsWithoutEntitlement(AppId, new RecordingTransport());

        Assert.True(CuratedClient.IsValid);
        Assert.False(CuratedClient.IsEntitled);

        var refresh = await Assert.ThrowsAsync<CuratedLaunchException>(() => Achievements.RefreshAsync());
        Assert.Equal(CuratedLaunchError.NotEntitled, refresh.Error);

        var stats = await Assert.ThrowsAsync<CuratedLaunchException>(() => Stats.RefreshAsync());
        Assert.Equal(CuratedLaunchError.NotEntitled, stats.Error);

        var cloud = await Assert.ThrowsAsync<CuratedLaunchException>(() =>
            Cloud.SyncOnLaunchAsync(System.IO.Path.GetTempPath()));
        Assert.Equal(CuratedLaunchError.NotEntitled, cloud.Error);

        var friends = await Assert.ThrowsAsync<CuratedLaunchException>(() => Friends.GetFriendsAsync());
        Assert.Equal(CuratedLaunchError.NotEntitled, friends.Error);

        var presence = await Assert.ThrowsAsync<CuratedLaunchException>(() => Presence.HeartbeatAsync());
        Assert.Equal(CuratedLaunchError.NotEntitled, presence.Error);

        var openId = await Assert.ThrowsAsync<CuratedLaunchException>(() =>
            CuratedClient.GetOpenIdIdTokenAsync());
        Assert.Equal(CuratedLaunchError.NotEntitled, openId.Error);

        var setStat = Assert.Throws<CuratedLaunchException>(() => Stats.SetStat("CardsCreated", 1));
        Assert.Equal(CuratedLaunchError.NotEntitled, setStat.Error);

        // Re-check ownership still works without prior entitlement verification.
        var transport = new StubLibraryTransport("[]");
        CuratedClient.InitForTestsWithoutEntitlement(AppId, transport);
        var entitlement = await Entitlements.CheckAsync();
        Assert.False(entitlement.IsEntitled);
    }

    [Fact]
    public async Task InitFromLauncher_marks_entitled()
    {
        await CuratedClient.InitFromLauncherAsync(
            AppId,
            LaunchEnv(),
            new OwnedHandler(),
            CancellationToken.None);

        Assert.True(CuratedClient.IsEntitled);
        Assert.NotNull(CuratedClient.Session);
    }

    [Fact]
    public void InitForTests_marks_entitled()
    {
        CuratedClient.InitForTests(AppId, new RecordingTransport());
        Assert.True(CuratedClient.IsEntitled);
    }

    private static Dictionary<string, string?> LaunchEnv() => new()
    {
        [LaunchContext.LauncherVariable] = "1",
        [LaunchContext.ContractVersionVariable] = "1",
        [LaunchContext.AppIdVariable] = "1002",
        [LaunchContext.SessionTokenVariable] = "tok",
    };

    private sealed class StubLibraryTransport : ITransport
    {
        private readonly string _library;

        public StubLibraryTransport(string library) => _library = library;

        public Task<string> SendAsync(
            string method,
            string relativePath,
            object? jsonBody,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_library);
    }

    private sealed class OwnedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/auth/me"
                ? "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"username\":\"player\",\"curated_id\":\"10000000000000001\"}"
                : "[{\"app_id\":1002,\"source\":\"grant\",\"acquired_at\":\"2026-10-20T08:30:00Z\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class NotOwnedHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/auth/me"
                ? "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"username\":\"player\",\"curated_id\":\"10000000000000001\"}"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
