using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class StatsTests : IDisposable
{
    public StatsTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task RefreshAsync_loads_All_and_GetStat()
    {
        var transport = new RecordingTransport((method, path, body) =>
        {
            Assert.Equal("GET", method);
            Assert.Equal("/games/app/1002/stats/progress", path);
            return """
                {
                  "app_id": 1002,
                  "stats": [
                    {
                      "api_name": "CardsCreated",
                      "display_name": "Cards Created",
                      "value_type": "int",
                      "value": 0,
                      "default_value": 0,
                      "min_value": 0,
                      "max_value": null,
                      "increment_only": true,
                      "set_by": "client",
                      "stored": false,
                      "extra_ignored": true
                    },
                    {
                      "api_name": "PlayTime",
                      "display_name": "Play Time",
                      "value_type": "float",
                      "value": 1.5,
                      "default_value": 0,
                      "increment_only": false,
                      "set_by": "client",
                      "stored": true
                    }
                  ]
                }
                """;
        });
        CuratedClient.InitForTests(1002, transport);

        await Stats.RefreshAsync();

        Assert.Equal(2, Stats.All.Count);
        Assert.Equal("CardsCreated", Stats.All[0].Name);
        Assert.Equal("Cards Created", Stats.All[0].DisplayName);
        Assert.False(Stats.All[0].Stored);
        Assert.True(Stats.All[0].IncrementOnly);
        Assert.True(Stats.GetStat("CardsCreated", out int cards));
        Assert.Equal(0, cards);
        Assert.True(Stats.GetStat("PlayTime", out float play));
        Assert.Equal(1.5f, play);
    }

    [Fact]
    public async Task SetStatAsync_puts_value_and_returns_unlocks()
    {
        var transport = new RecordingTransport((method, path, body) =>
        {
            Assert.Equal("PUT", method);
            Assert.Equal("/games/app/9/stats/CardsCreated", path);
            Assert.IsType<SetStatRequestWire>(body);
            Assert.Equal(10d, ((SetStatRequestWire)body!).Value);
            return """
                {
                  "app_id": 9,
                  "stat": {
                    "api_name": "CardsCreated",
                    "display_name": "Cards Created",
                    "value_type": "int",
                    "value": 10,
                    "default_value": 0,
                    "min_value": 0,
                    "max_value": null,
                    "increment_only": true,
                    "set_by": "client",
                    "stored": true
                  },
                  "previous_value": 0,
                  "unlocked_achievements": ["make_10_cards"]
                }
                """;
        });
        CuratedClient.InitForTests(9, transport);

        var result = await Stats.SetStatAsync("CardsCreated", 10);

        Assert.Equal(10, result.Value);
        Assert.Equal(0, result.PreviousValue);
        Assert.Null(result.Delta);
        Assert.True(result.Stat.Stored);
        Assert.Equal(new[] { "make_10_cards" }, result.UnlockedAchievements);
        Assert.True(Stats.GetStat("CardsCreated", out int cards));
        Assert.Equal(10, cards);
    }

    [Fact]
    public async Task IncrementStatAsync_posts_delta()
    {
        var transport = new RecordingTransport((method, path, body) =>
        {
            Assert.Equal("POST", method);
            Assert.Equal("/games/app/3/stats/CardsCreated/increment", path);
            var req = Assert.IsType<IncrementStatRequestWire>(body);
            Assert.Equal(3d, req.Delta);
            return """
                {
                  "app_id": 3,
                  "stat": {
                    "api_name": "CardsCreated",
                    "display_name": "Cards Created",
                    "value_type": "int",
                    "value": 13,
                    "default_value": 0,
                    "increment_only": true,
                    "set_by": "client",
                    "stored": true
                  },
                  "previous_value": 10,
                  "delta": 3,
                  "unlocked_achievements": []
                }
                """;
        });
        CuratedClient.InitForTests(3, transport);

        var result = await Stats.IncrementStatAsync("CardsCreated", 3);

        Assert.Equal(13, result.Value);
        Assert.Equal(10, result.PreviousValue);
        Assert.Equal(3, result.Delta);
        Assert.Empty(result.UnlockedAchievements);
    }

    [Fact]
    public async Task StoreStatsAsync_batch_posts_store_body()
    {
        object? captured = null;
        var transport = new RecordingTransport((method, path, body) =>
        {
            Assert.Equal("POST", method);
            Assert.Equal("/games/app/1002/stats/store", path);
            captured = body;
            return """
                {
                  "app_id": 1002,
                  "updated": [
                    {
                      "stat": {
                        "api_name": "CardsCreated",
                        "display_name": "Cards Created",
                        "value_type": "int",
                        "value": 12,
                        "default_value": 0,
                        "increment_only": true,
                        "set_by": "client",
                        "stored": true
                      },
                      "previous_value": 11
                    },
                    {
                      "stat": {
                        "api_name": "CompletedChallenges",
                        "display_name": "Completed Challenges",
                        "value_type": "int",
                        "value": 1,
                        "default_value": 0,
                        "increment_only": false,
                        "set_by": "client",
                        "stored": true
                      },
                      "previous_value": 0
                    }
                  ],
                  "unlocked_achievements": ["challenge_1"]
                }
                """;
        });
        CuratedClient.InitForTests(1002, transport);

        var result = await Stats.StoreStatsAsync(
            StatUpdate.Set("CardsCreated", 12),
            StatUpdate.Increment("CompletedChallenges", 1));

        var req = Assert.IsType<StoreStatsRequestWire>(captured);
        Assert.Equal(2, req.Stats.Count);
        Assert.Equal("CardsCreated", req.Stats[0].ApiName);
        Assert.Equal(12d, req.Stats[0].Value);
        Assert.Null(req.Stats[0].Delta);
        Assert.Equal("CompletedChallenges", req.Stats[1].ApiName);
        Assert.Equal(1d, req.Stats[1].Delta);
        Assert.Null(req.Stats[1].Value);

        Assert.Equal(2, result.Updated.Count);
        Assert.Equal(12, result.Updated[0].Value);
        Assert.Equal(11, result.Updated[0].PreviousValue);
        Assert.Equal(new[] { "challenge_1" }, result.UnlockedAchievements);
        Assert.True(Stats.GetStat("CompletedChallenges", out int challenges));
        Assert.Equal(1, challenges);
    }

    [Fact]
    public async Task SetStat_then_StoreStatsAsync_flushes_pending()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(7, transport);

        Stats.SetStat("CardsCreated", 5);
        Assert.True(Stats.GetStat("CardsCreated", out int pending));
        Assert.Equal(5, pending);

        var result = await Stats.StoreStatsAsync();

        Assert.Single(transport.Calls);
        Assert.Equal("POST", transport.Calls[0].Method);
        Assert.Equal("/games/app/7/stats/store", transport.Calls[0].RelativePath);
        var req = Assert.IsType<StoreStatsRequestWire>(transport.Calls[0].Body);
        Assert.Single(req.Stats);
        Assert.Equal("CardsCreated", req.Stats[0].ApiName);
        Assert.Equal(5d, req.Stats[0].Value);
        Assert.Single(result.Updated);
        Assert.Equal(5, result.Updated[0].Value);
    }

    [Fact]
    public async Task SetStatAsync_escapes_api_name()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(3, transport);

        await Stats.SetStatAsync("a/b", 1);

        Assert.Equal("/games/app/3/stats/a%2Fb", transport.Calls[0].RelativePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Stat_rejects_empty_name(string? name)
    {
        Assert.Throws<ArgumentException>(() => new Stat(name!));
    }

    [Fact]
    public void StatUpdate_requires_exactly_one_of_value_or_delta()
    {
        Assert.Throws<ArgumentException>(() => StatUpdate.Set("", 1));
        _ = StatUpdate.Set("a", 1);
        _ = StatUpdate.Increment("a", 2);
    }

    [Fact]
    public async Task StoreStatsAsync_rejects_empty_batch()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(1, transport);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Stats.StoreStatsAsync(Array.Empty<StatUpdate>()));
    }
}
