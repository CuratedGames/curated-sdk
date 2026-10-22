using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Curated.Sdk.Tests;

/// <summary>Runs every case of contracts/fixtures/conformance.v1.json against the C# binding.</summary>
public class LaunchContractConformanceTests : IDisposable
{
    private static readonly Lazy<JsonDocument> Fixture = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "conformance.v1.json"))));

    public LaunchContractConformanceTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    public static IEnumerable<object[]> CaseNames() =>
        Fixture.Value.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Fact]
    public void Fixture_targets_supported_contract_version()
    {
        Assert.Equal(LaunchContext.SupportedContractVersion, Fixture.Value.RootElement.GetProperty("contract_version").GetInt32());
        Assert.Equal(
            CuratedClient.DefaultHubUrl.AbsoluteUri.TrimEnd('/'),
            Fixture.Value.RootElement.GetProperty("default_api_base").GetString());
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Case_matches_contract(string name)
    {
        var root = Fixture.Value.RootElement;
        var gameAppId = root.GetProperty("game_app_id").GetInt64();
        var testCase = root.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var expect = testCase.GetProperty("expect");

        var env = testCase.GetProperty("env").EnumerateObject()
            .ToDictionary(p => p.Name, p => (string?)p.Value.GetString(), StringComparer.Ordinal);
        var handler = new FixtureHandler(testCase.GetProperty("responses"));

        CuratedSession? session = null;
        CuratedLaunchException? failure = null;
        try
        {
            session = await CuratedClient.InitFromLauncherAsync(gameAppId, env, handler);
        }
        catch (CuratedLaunchException e)
        {
            failure = e;
        }

        var expectedRequests = expect.GetProperty("requests").EnumerateArray().Select(r => r.GetString()!).ToList();
        Assert.Equal(expectedRequests, handler.Requests);

        if (expect.GetProperty("ok").GetBoolean())
        {
            Assert.Null(failure);
            Assert.NotNull(session);
            Assert.True(CuratedClient.IsValid);
            Assert.Same(session, CuratedClient.Session);
            Assert.All(handler.Authorizations, a => Assert.Equal(expect.GetProperty("authorization").GetString(), a));
            Assert.Equal(expect.GetProperty("app_id").GetInt64(), session!.AppId);
            Assert.Equal(Guid.Parse(expect.GetProperty("user_id").GetString()!), session.UserId);
            Assert.Equal(expect.GetProperty("username").GetString(), session.Username);
            Assert.Equal(NullableString(expect.GetProperty("language")), session.Language);
            Assert.Equal(expect.GetProperty("entitled").GetBoolean(), session.Entitlement.IsEntitled);
            Assert.Equal(expect.GetProperty("source").GetString(), session.Entitlement.Source);
            Assert.Equal(
                DateTimeOffset.Parse(expect.GetProperty("acquired_at").GetString()!),
                session.Entitlement.AcquiredAt);
            Assert.Equal(
                expect.GetProperty("api_base").GetString(),
                LaunchContext.ParseVariables(env, gameAppId).ApiBaseText);
        }
        else
        {
            Assert.NotNull(failure);
            Assert.Equal(expect.GetProperty("error").GetString(), failure!.Code);
            Assert.False(CuratedClient.IsValid);
            Assert.Null(CuratedClient.Session);
        }
    }

    private static string? NullableString(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : e.GetString();

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly JsonElement _responses;

        public FixtureHandler(JsonElement responses) => _responses = responses;

        public List<string> Requests { get; } = new();

        public List<string?> Authorizations { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri!.AbsoluteUri}");
            Authorizations.Add(request.Headers.Authorization?.ToString());
            if (!_responses.TryGetProperty(request.RequestUri.AbsolutePath, out var response))
            {
                throw new InvalidOperationException($"Unexpected request {request.RequestUri}");
            }

            if (response.TryGetProperty("network_error", out var net) && net.GetBoolean())
            {
                throw new HttpRequestException("simulated network failure");
            }

            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)response.GetProperty("status").GetInt32())
            {
                Content = new StringContent(response.GetProperty("body").GetRawText(), Encoding.UTF8, "application/json"),
            });
        }
    }
}
