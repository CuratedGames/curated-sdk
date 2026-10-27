using System;
using System.Collections.Generic;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class JsonTests
{
    [Fact]
    public void Serialize_uses_snake_case_and_omits_nulls()
    {
        var json = Json.Serialize(new PresenceRow
        {
            UserId = Guid.Parse("0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11"),
            Username = null,
            Status = "in_game",
            RichText = "Level \"3\"\n",
            AppId = 1002,
            UpdatedAt = new DateTimeOffset(2026, 10, 22, 8, 30, 0, TimeSpan.Zero),
        });

        Assert.Equal(
            "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"status\":\"in_game\",\"rich_text\":\"Level \\\"3\\\"\\n\",\"app_id\":1002,\"updated_at\":\"2026-10-22T08:30:00.0000000+00:00\"}",
            json);
    }

    [Fact]
    public void Roundtrip_matches_System_Text_Json_reading()
    {
        var json = Json.Serialize(new { relative_path = "saves/é.sav", size_bytes = 12L, ok = true, ratio = 0.5 });
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal("saves/é.sav", doc.RootElement.GetProperty("relative_path").GetString());
        Assert.Equal(12, doc.RootElement.GetProperty("size_bytes").GetInt64());
        Assert.True(doc.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(0.5, doc.RootElement.GetProperty("ratio").GetDouble());
    }

    [Fact]
    public void Deserialize_ignores_unknown_fields_and_reads_nested_lists()
    {
        var result = Json.Deserialize<AchievementProgressResult>(
            "{ \"app_id\": 1002, \"unlocked\": 1, \"total\": 2, \"extra\": {\"x\": [1, 2]},"
            + " \"achievements\": [ {\"api_name\": \"win\\u0021\", \"unlocked\": true, \"unlocked_at\": \"2026-10-20T08:30:00Z\"},"
            + " {\"api_name\": \"lose\", \"unlocked\": false, \"unlocked_at\": null} ] }");

        Assert.Equal(1002, result.AppId);
        Assert.Equal(2, result.Total);
        Assert.Equal("win!", result.Achievements[0].ApiName);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero), result.Achievements[0].UnlockedAt);
        Assert.Null(result.Achievements[1].UnlockedAt);
    }

    [Fact]
    public void Deserialize_top_level_list()
    {
        var items = Json.Deserialize<List<EntitlementWire>>("[{\"app_id\":1002,\"source\":\"primary\"},{\"app_id\":7}]");
        Assert.Equal(2, items.Count);
        Assert.Equal("primary", items[0].Source);
        Assert.Null(items[1].Source);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"a\":}")]
    [InlineData("[1,]")]
    [InlineData("\"unterminated")]
    [InlineData("{} trailing")]
    public void Parse_rejects_malformed_json(string text)
    {
        Assert.Throws<FormatException>(() => Json.Parse(text));
    }

    [Fact]
    public void Deserialize_rejects_wrong_shapes()
    {
        Assert.Throws<FormatException>(() => Json.Deserialize<SessionUserWire>("[]"));
        Assert.Throws<FormatException>(() => Json.Deserialize<SessionUserWire>("{\"user_id\": 5}"));
    }

    [Fact]
    public void Deserialize_session_user_reads_public_curated_id()
    {
        var user = Json.Deserialize<SessionUserWire>(
            "{\"user_id\":\"0b4c6f8e-6f2a-4f3d-9a51-2f0e8f6a1c11\",\"username\":\"player1\",\"curated_id\":\"10000000000000001\"}");
        Assert.Equal("10000000000000001", user.CuratedId);
        Assert.Equal("player1", user.Username);
    }
}
