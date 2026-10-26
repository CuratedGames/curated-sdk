using Xunit;

namespace Curated.Sdk.Tests;

public class LocalAccountSessionTests
{
    [Fact]
    public void ReadJsonString_reads_token_field()
    {
        const string json = """
            {
              "token": "abc",
              "username": "player1"
            }
            """;

        Assert.Equal("abc", LocalAccountSession.ReadJsonString(json, "token"));
    }

    [Fact]
    public void ReadJsonString_missing_or_empty_is_null()
    {
        Assert.Null(LocalAccountSession.ReadJsonString(null, "token"));
        Assert.Null(LocalAccountSession.ReadJsonString("{}", "token"));
        Assert.Null(LocalAccountSession.ReadJsonString("""{"token":"  "}""", "token"));
    }
}
