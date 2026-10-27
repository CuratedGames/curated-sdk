using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class OverlayNotifierTests
{
    [Fact]
    public void BuildPayload_includes_toast_fields()
    {
        var json = OverlayNotifier.BuildPayload(
            "tok",
            1002,
            "as_it_comes",
            "As it Comes",
            "Complete mission 7",
            "https://cdn.example/a.jpg",
            4.9);

        Assert.Contains("\"type\":\"achievement_unlocked\"", json);
        Assert.Contains("\"app_id\":1002", json);
        Assert.Contains("\"api_name\":\"as_it_comes\"", json);
        Assert.Contains("\"display_name\":\"As it Comes\"", json);
        Assert.Contains("\"icon_url\":\"https://cdn.example/a.jpg\"", json);
        Assert.Contains("\"global_unlock_percent\":4.9", json);
        Assert.Contains("\"token\":\"tok\"", json);
    }

    [Fact]
    public void BuildPayload_escapes_quotes()
    {
        var json = OverlayNotifier.BuildPayload(
            "t\"ok",
            1,
            "n",
            "He said \"hi\"",
            "line\nbreak",
            null,
            null);
        Assert.Contains("\\\"hi\\\"", json);
        Assert.Contains("\\n", json);
    }

    [Theory]
    [InlineData("127.0.0.1:4050", "127.0.0.1", 4050)]
    [InlineData("http://127.0.0.1:9", "127.0.0.1", 9)]
    public void TryParseEndpoint_accepts_loopback(string raw, string host, int port)
    {
        Assert.True(OverlayNotifier.TryParseEndpoint(raw, out var h, out var p));
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData("")]
    [InlineData("127.0.0.1")]
    [InlineData(":80")]
    [InlineData("127.0.0.1:0")]
    public void TryParseEndpoint_rejects_bad(string raw)
    {
        Assert.False(OverlayNotifier.TryParseEndpoint(raw, out _, out _));
    }
}
