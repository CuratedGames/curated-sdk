using System;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class AchievementsTests : IDisposable
{
    public AchievementsTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task TriggerAsync_unlocks_and_records_path()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(1001, transport);

        var newly = await new Achievement("win_level_1").TriggerAsync();

        Assert.True(newly);
        Assert.Single(transport.Calls);
        Assert.Equal("POST", transport.Calls[0].Method);
        Assert.Equal(
            "/games/app/1001/achievements/win_level_1/unlock",
            transport.Calls[0].RelativePath);
    }

    [Fact]
    public async Task Achievements_TriggerAsync_shortcut()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(9, transport);

        Assert.True(await Achievements.TriggerAsync("boss_kill"));
        Assert.Equal(
            "/games/app/9/achievements/boss_kill/unlock",
            transport.Calls[0].RelativePath);
    }

    [Fact]
    public async Task RefreshAsync_loads_All()
    {
        var transport = new RecordingTransport((method, path, body) =>
        {
            if (path.EndsWith("/achievements/progress", StringComparison.Ordinal))
            {
                return """
                    {
                      "app_id": 3,
                      "unlocked": 1,
                      "total": 2,
                      "achievements": [
                        {
                          "api_name": "a",
                          "display_name": "A",
                          "description": "First",
                          "unlocked": true
                        },
                        {
                          "api_name": "b",
                          "display_name": "B",
                          "description": "Second",
                          "unlocked": false
                        }
                      ]
                    }
                    """;
            }

            return "{}";
        });
        CuratedClient.InitForTests(3, transport);

        await Achievements.RefreshAsync();

        Assert.Equal(2, Achievements.All.Count);
        Assert.Equal("a", Achievements.All[0].Name);
        Assert.True(Achievements.All[0].State);
        Assert.Equal("B", Achievements.All[1].DisplayName);
        Assert.False(Achievements.All[1].State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Achievement_rejects_empty_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Achievement(name!));
    }

    [Fact]
    public async Task TriggerAsync_escapes_name()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(3, transport);

        await new Achievement("a/b").TriggerAsync();

        Assert.Equal(
            "/games/app/3/achievements/a%2Fb/unlock",
            transport.Calls[0].RelativePath);
    }
}
