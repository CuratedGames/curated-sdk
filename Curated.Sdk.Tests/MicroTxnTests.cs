using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

public class MicroTxnTests : IDisposable
{
    public MicroTxnTests() => CuratedClient.Shutdown();

    public void Dispose() => CuratedClient.Shutdown();

    [Fact]
    public async Task AuthorizeAsync_posts_player_decision_without_publisher_key()
    {
        Dictionary<string, object>? sent = null;
        var transport = new RecordingTransport((method, path, body) =>
        {
            Assert.Equal("POST", method);
            Assert.Equal("/ICuratedMicroTxn/AuthorizeTxn/v1/", path);
            Assert.DoesNotContain("key=", path, StringComparison.OrdinalIgnoreCase);
            sent = Assert.IsType<Dictionary<string, object>>(body);
            return """
                {
                  "response": {
                    "result": "OK",
                    "params": { "orderid": "55", "transid": "77", "authorized": 1 }
                  }
                }
                """;
        });
        CuratedClient.InitForTests(1002, transport);

        var result = await MicroTxn.AuthorizeAsync(55, 77, authorized: true);

        Assert.NotNull(sent);
        Assert.Equal(55L, sent!["orderid"]);
        Assert.Equal(77L, sent["transid"]);
        Assert.Equal(1002L, sent["appid"]);
        Assert.Equal(true, sent["authorized"]);
        Assert.False(sent.ContainsKey("key"));
        Assert.Equal(new[] { "orderid", "transid", "appid", "authorized" }, sent.Keys);
        var json = Json.Serialize(sent);
        Assert.DoesNotContain("\"key\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(MicroTxnAuthorizationStatus.Authorized, result.Status);
        Assert.Equal(55, result.OrderId);
        Assert.Equal(77, result.TransId);
        Assert.Null(result.ErrorCode);
        Assert.Null(result.ErrorDescription);
    }

    [Fact]
    public async Task AuthorizeAsync_denied_when_player_declines()
    {
        var transport = new RecordingTransport((_, _, _) => """
            {
              "response": {
                "result": "OK",
                "params": { "orderid": 9, "transid": 8, "authorized": 0 }
              }
            }
            """);
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(9, 8, authorized: false);

        Assert.Equal(MicroTxnAuthorizationStatus.Denied, result.Status);
        Assert.Equal(9, result.OrderId);
        Assert.Equal(8, result.TransId);
    }

    [Fact]
    public async Task AuthorizeAsync_trusts_response_decision()
    {
        var transport = new RecordingTransport((_, _, _) => """
            {"response":{"result":"OK","params":{"authorized":false}}}
            """);
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(4, 5, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Denied, result.Status);
        Assert.Equal(4, result.OrderId);
        Assert.Equal(5, result.TransId);
    }

    [Fact]
    public async Task AuthorizeAsync_ok_without_authorized_keeps_submitted_decision()
    {
        var transport = new RecordingTransport((_, _, _) => """
            {"response":{"result":"ok","params":{"orderid":1,"transid":2}}}
            """);
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: false);

        Assert.Equal(MicroTxnAuthorizationStatus.Denied, result.Status);
    }

    [Fact]
    public async Task AuthorizeAsync_failure_envelope_is_error_not_exception()
    {
        var transport = new RecordingTransport((_, _, _) => """
            {
              "response": {
                "result": "Failure",
                "error": { "errorcode": 7, "errordesc": "Order is not pending" }
              }
            }
            """);
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(11, 12, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal(7, result.ErrorCode);
        Assert.Equal("Order is not pending", result.ErrorDescription);
        Assert.Equal(11, result.OrderId);
        Assert.Equal(12, result.TransId);
    }

    [Fact]
    public async Task AuthorizeAsync_http_failure_envelope_is_error()
    {
        var transport = new ThrowingTransport(
            """{"response":{"result":"Failure","error":{"errorcode":2,"errordesc":"Transaction not found"}}}""");
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Equal(2, result.ErrorCode);
        Assert.Equal("Transaction not found", result.ErrorDescription);
    }

    [Fact]
    public async Task AuthorizeAsync_http_error_without_envelope_throws()
    {
        CuratedClient.InitForTests(3, new ThrowingTransport("{\"error\":\"nope\"}"));

        var ex = await Assert.ThrowsAsync<CuratedException>(() =>
            MicroTxn.AuthorizeAsync(1, 2, authorized: true));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task AuthorizeAsync_ignores_publisher_key_if_response_includes_one()
    {
        var transport = new RecordingTransport((_, _, _) => """
            {"response":{"result":"OK","params":{"orderid":1,"transid":2,"authorized":1,"key":"secret-publisher-key"}}}
            """);
        CuratedClient.InitForTests(3, transport);

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Authorized, result.Status);
        Assert.Null(result.ErrorDescription);
        Assert.DoesNotContain(
            typeof(MicroTxnAuthorization).GetProperties(),
            property => property.Name.Contains("Key", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AuthorizeAsync_unreadable_body_is_error()
    {
        CuratedClient.InitForTests(3, new RecordingTransport((_, _, _) => "not-json"));

        var result = await MicroTxn.AuthorizeAsync(1, 2, authorized: true);

        Assert.Equal(MicroTxnAuthorizationStatus.Error, result.Status);
        Assert.Null(result.ErrorCode);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorDescription));
    }

    [Fact]
    public async Task AuthorizeAsync_requires_init_and_positive_ids()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            MicroTxn.AuthorizeAsync(1, 2, true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MicroTxn.AuthorizeAsync(0, 2, true).GetAwaiter().GetResult());

        CuratedClient.InitForTestsWithoutEntitlement(1002, new RecordingTransport());
        var ex = await Assert.ThrowsAsync<CuratedLaunchException>(() =>
            MicroTxn.AuthorizeAsync(1, 2, true));
        Assert.Equal(CuratedLaunchError.NotEntitled, ex.Error);
    }

    private sealed class ThrowingTransport : ITransport
    {
        private readonly string _body;

        public ThrowingTransport(string body) => _body = body;

        public Task<string> SendAsync(
            string method,
            string relativePath,
            object? jsonBody,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal("/ICuratedMicroTxn/AuthorizeTxn/v1/", relativePath);
            var sent = Assert.IsType<Dictionary<string, object>>(jsonBody);
            Assert.False(sent.ContainsKey("key"));
            throw new CuratedException(HttpStatusCode.BadRequest, _body, "request failed");
        }
    }
}
