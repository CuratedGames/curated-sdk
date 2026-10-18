using System;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class FriendsTests : IDisposable
{
    public FriendsTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task GetFriendsAsync_merges_presence()
    {
        var friendId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var transport = new RecordingTransport((method, path, body) =>
        {
            if (path == "/friends")
            {
                return $$"""
                    [{"user_id":"{{friendId}}","username":"alice","status":"accepted"}]
                    """;
            }

            if (path == "/friends/presence")
            {
                return $$"""
                    [{
                      "user_id":"{{friendId}}",
                      "username":"alice",
                      "status":"in_game",
                      "rich_text":"Level 3",
                      "app_id":42,
                      "updated_at":"2026-10-18T00:00:00Z"
                    }]
                    """;
            }

            return "[]";
        });
        CuratedClient.InitForTests(42, transport);

        var friends = await Friends.GetFriendsAsync();

        Assert.Single(friends);
        Assert.Equal("alice", friends[0].Name);
        Assert.True(friends[0].IsOnline);
        Assert.Equal("in_game", friends[0].PresenceStatus);
        Assert.Equal("Level 3", friends[0].RichText);
        Assert.Equal(42, friends[0].AppId);
    }

    [Fact]
    public async Task Request_and_Accept_use_expected_paths()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(1, transport);
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await Friends.RequestAsync("bob");
        await Friends.AcceptAsync(other);

        Assert.Equal("/friends/request", transport.Calls[0].RelativePath);
        Assert.Equal("POST", transport.Calls[0].Method);
        Assert.Equal($"/friends/{other:D}/accept", transport.Calls[1].RelativePath);
    }

    [Fact]
    public async Task Presence_SetAsync_sends_app_id_when_in_game()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(99, transport);

        await Presence.SetAsync("in_game", "Boss fight");

        Assert.Equal("PUT", transport.Calls[0].Method);
        Assert.Equal("/presence", transport.Calls[0].RelativePath);
        var body = Assert.IsType<SetPresenceRequest>(transport.Calls[0].Body);
        Assert.Equal("in_game", body.Status);
        Assert.Equal("Boss fight", body.RichText);
        Assert.Equal(99, body.AppId);
    }

    [Fact]
    public async Task Presence_HeartbeatAsync_posts()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(1, transport);

        await Presence.HeartbeatAsync();

        Assert.Equal("POST", transport.Calls[0].Method);
        Assert.Equal("/presence/heartbeat", transport.Calls[0].RelativePath);
    }
}
