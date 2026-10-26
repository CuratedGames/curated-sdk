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

/// <summary>Runs contracts/fixtures/game-language.v1.json against the C# binding.</summary>
public class GameLanguageTests : IDisposable
{
    private static readonly Lazy<JsonDocument> Fixture = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "game-language.v1.json"))));

    private readonly string? _savedLanguage = Environment.GetEnvironmentVariable(LaunchContext.LanguageVariable);

    public GameLanguageTests() => CuratedClient.Shutdown();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LaunchContext.LanguageVariable, _savedLanguage);
        CuratedClient.Shutdown();
    }

    public static IEnumerable<object[]> CaseNames() =>
        Fixture.Value.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Fact]
    public void Table_matches_fixture()
    {
        var root = Fixture.Value.RootElement;
        Assert.Equal(GameLanguages.DefaultLanguage, root.GetProperty("default_language").GetString());
        var expected = root.GetProperty("languages").EnumerateArray().ToList();
        Assert.Equal(expected.Count, GameLanguages.All.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            var e = expected[i];
            var actual = GameLanguages.All[i];
            Assert.Equal(e.GetProperty("code").GetString(), actual.Code);
            Assert.Equal(e.GetProperty("name").GetString(), actual.EnglishName);
            Assert.Equal(e.GetProperty("steam").GetString(), actual.SteamApiName);
            Assert.Equal(
                e.GetProperty("aliases").EnumerateArray().Select(a => a.GetString()).ToList(),
                actual.Aliases.ToList());
        }
    }

    [Theory]
    [MemberData(nameof(CaseNames))]
    public void Case_matches_fixture(string name)
    {
        var testCase = Fixture.Value.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == name);
        var env = testCase.GetProperty("env");
        var raw = env.TryGetProperty(LaunchContext.LanguageVariable, out var v) ? v.GetString() : null;
        var expect = testCase.GetProperty("expect");

        Environment.SetEnvironmentVariable(LaunchContext.LanguageVariable, raw);
        Assert.Equal(expect.GetProperty("language").GetString(), CuratedClient.GetCurrentGameLanguage());
        Assert.Equal(expect.GetProperty("steam").GetString(), CuratedClient.GetCurrentGameLanguageSteamApiName());
    }

    [Fact]
    public void Steam_helpers_round_trip_every_language()
    {
        foreach (var language in GameLanguages.All)
        {
            Assert.Equal(language.SteamApiName, GameLanguages.ToSteamApiName(language.Code));
            Assert.Equal(language.Code, GameLanguages.FromSteamApiName(language.SteamApiName));
            Assert.Equal(language.Code, GameLanguages.Normalize(language.Code.ToUpperInvariant()));
        }

        Assert.Equal("es-419", GameLanguages.FromSteamApiName(" LATAM "));
        Assert.Null(GameLanguages.FromSteamApiName("es"));
        Assert.Null(GameLanguages.FromSteamApiName(null));
        Assert.Null(GameLanguages.ToSteamApiName("klingon"));
        Assert.Null(GameLanguages.Normalize("  "));
    }

    [Fact]
    public void Codes_are_lowercase_and_unique()
    {
        var keys = GameLanguages.All.SelectMany(l => new[] { l.Code }.Concat(l.Aliases)).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.All(keys, k => Assert.Matches("^[a-z0-9-]+$", k));
        Assert.Equal(
            GameLanguages.All.Count,
            GameLanguages.All.Select(l => l.SteamApiName).Distinct().Count());
    }

    [Fact]
    public async Task Session_exposes_normalized_language_with_fallback()
    {
        var env = new Dictionary<string, string?>
        {
            [LaunchContext.LauncherVariable] = "1",
            [LaunchContext.ContractVersionVariable] = "1",
            [LaunchContext.AppIdVariable] = "1002",
            [LaunchContext.SessionTokenVariable] = "tok",
            [LaunchContext.LanguageVariable] = "es-ES",
        };
        var session = await CuratedClient.InitFromLauncherAsync(1002, env, new OkHandler());
        Assert.Equal("es-ES", session.Language);
        Assert.Equal("es", session.CurrentGameLanguage);
        CuratedClient.Shutdown();

        env.Remove(LaunchContext.LanguageVariable);
        session = await CuratedClient.InitFromLauncherAsync(1002, env, new OkHandler());
        Assert.Null(session.Language);
        Assert.Equal("en", session.CurrentGameLanguage);
    }

    [Fact]
    public void Works_without_init()
    {
        Environment.SetEnvironmentVariable(LaunchContext.LanguageVariable, "it");
        Assert.False(CuratedClient.IsValid);
        Assert.Equal("it", CuratedClient.GetCurrentGameLanguage());
        Assert.Equal("italian", CuratedClient.GetCurrentGameLanguageSteamApiName());
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.RequestUri!.AbsolutePath == "/auth/me"
                ? "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"username\":\"p\",\"curated_id\":\"72057598332895237\"}"
                : "[{\"app_id\":1002,\"source\":\"grant\",\"acquired_at\":\"2026-10-20T08:30:00Z\"}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
