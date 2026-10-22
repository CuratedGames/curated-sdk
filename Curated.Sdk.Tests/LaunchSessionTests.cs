using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class LaunchSessionTests : IDisposable
{
    private const long AppId = 1002;

    public LaunchSessionTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    private static Dictionary<string, string?> LaunchEnv(string? apiBase = null) => new()
    {
        [LaunchContext.LauncherVariable] = "1",
        [LaunchContext.ContractVersionVariable] = "1",
        [LaunchContext.AppIdVariable] = "1002",
        [LaunchContext.SessionTokenVariable] = "tok",
        [LaunchContext.ApiBaseVariable] = apiBase,
    };

    [Fact]
    public async Task Entitlement_recheck_reports_revoked_copy()
    {
        var transport = new StubTransport(new Dictionary<string, string>
        {
            ["/library"] = "[{\"app_id\":1002,\"source\":\"primary\",\"acquired_at\":\"2026-10-20T08:30:00Z\"}]",
        });
        CuratedClient.InitForTests(AppId, transport);

        var owned = await Entitlements.CheckAsync();
        Assert.True(owned.IsEntitled);
        Assert.Equal("primary", owned.Source);

        transport.Bodies["/library"] = "[]";
        var revoked = await Entitlements.CheckAsync();
        Assert.False(revoked.IsEntitled);
        Assert.Null(revoked.Source);
        Assert.Null(revoked.AcquiredAt);
    }

    [Fact]
    public async Task Shutdown_clears_session()
    {
        await CuratedClient.InitFromLauncherAsync(AppId, LaunchEnv(), new OkHandler());
        Assert.NotNull(CuratedClient.Session);

        CuratedClient.Shutdown();
        Assert.Null(CuratedClient.Session);
        Assert.False(CuratedClient.IsValid);
    }

    [Fact]
    public async Task Init_from_launcher_twice_throws_and_keeps_first_session()
    {
        var first = await CuratedClient.InitFromLauncherAsync(AppId, LaunchEnv(), new OkHandler());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CuratedClient.InitFromLauncherAsync(AppId, LaunchEnv(), new OkHandler()));
        Assert.Same(first, CuratedClient.Session);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://api.example.test")]
    public void Invalid_api_base_is_rejected(string apiBase)
    {
        var e = Assert.Throws<CuratedLaunchException>(() => LaunchContext.ParseVariables(LaunchEnv(apiBase), AppId));
        Assert.Equal("not_launched_by_curated", e.Code);
    }

    [Fact]
    public void Session_token_is_not_public()
    {
        var publicNames = typeof(LaunchContext).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain("SessionToken", publicNames);
    }

    [Fact]
    public void Error_codes_are_unique_and_snake_case()
    {
        var codes = Enum.GetValues(typeof(CuratedLaunchError)).Cast<CuratedLaunchError>()
            .Select(CuratedLaunchException.ToCode)
            .ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[a-z_]+$", c));
    }

    private sealed class StubTransport : ITransport
    {
        public StubTransport(Dictionary<string, string> bodies) => Bodies = bodies;

        public Dictionary<string, string> Bodies { get; }

        public Task<string> SendAsync(string method, string relativePath, object? jsonBody, CancellationToken cancellationToken = default) =>
            Task.FromResult(Bodies[relativePath]);
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/auth/me"
                ? "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"username\":\"p\"}"
                : "[{\"app_id\":1002,\"source\":\"grant\",\"acquired_at\":\"2026-10-20T08:30:00Z\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
