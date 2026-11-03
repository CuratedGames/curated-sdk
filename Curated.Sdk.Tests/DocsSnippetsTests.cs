using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Curated.Sdk.Internal;
using Xunit;

namespace Curated.Sdk.Tests;

/// <summary>
/// Code shown on docs.curated.games. The docs import the #region blocks below, so every
/// snippet on the site compiles and runs here. Keep regions short and free of test-only calls.
/// </summary>
[Collection(OverlayEnvCollection.Name)]
public class DocsSnippetsTests : IDisposable
{
    private const long AppId = 1002;

    public DocsSnippetsTests() => CuratedClient.Shutdown();

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, null);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, null);
        CuratedClient.Shutdown();
    }

    private static async Task<string> InitFromLauncher()
    {
        // The docs show the launcher path; tests swap the environment for a recorded transport.
        try
        {
            #region init-from-launcher
            CuratedSession session = await CuratedClient.InitFromLauncherAsync(1002);
            Console.WriteLine($"Signed in as {session.Username} ({session.CuratedId})");
            #endregion init-from-launcher
            return "ok";
        }
        catch (CuratedLaunchException e)
        {
            #region launch-error
            // e.Code: not_launched_by_curated, not_entitled, session_invalid, ...
            Console.WriteLine($"Curated: {e.Code}");
            #endregion launch-error
            return e.Code;
        }
    }

    [Fact]
    public async Task Init_snippet_reports_contract_code_outside_the_launcher()
    {
        var saved = Environment.GetEnvironmentVariable(LaunchContext.LauncherVariable);
        Environment.SetEnvironmentVariable(LaunchContext.LauncherVariable, null);
        try
        {
            Assert.Equal("not_launched_by_curated", await InitFromLauncher());
        }
        finally
        {
            Environment.SetEnvironmentVariable(LaunchContext.LauncherVariable, saved);
        }
    }

    [Fact]
    public async Task Achievements_snippet_runs()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(AppId, transport);

        #region achievements
        await Achievements.RefreshAsync();
        bool newlyUnlocked = await new Achievement("win_level_1").TriggerAsync();
        // Or: await Achievements.TriggerAsync("win_level_1");
        #endregion achievements

        Assert.True(newlyUnlocked);
        Assert.Contains(transport.Calls, c => c.RelativePath == "/games/app/1002/achievements/win_level_1/unlock");
    }

    [Fact]
    public async Task Stats_snippet_runs()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(AppId, transport);

        #region stats
        await Stats.RefreshAsync();

        // Steam-style: change local values, then store them in one call.
        Stats.SetStat("CardsCreated", 10);
        await Stats.StoreStatsAsync();

        // Or write one stat right away.
        await Stats.SetStatAsync("CardsCreated", 10);
        await Stats.IncrementStatAsync("CardsCreated", 1);
        #endregion stats

        Assert.Contains(transport.Calls, c => c.RelativePath.EndsWith("/stats/CardsCreated/increment", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cloud_save_snippet_runs()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(AppId, transport);
        var saveFolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "curated-docs-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(saveFolder, "saves"));
        System.IO.File.WriteAllText(System.IO.Path.Combine(saveFolder, "saves", "slot1.sav"), "save");
        try
        {
            #region cloud-save
            // On launch: download newer Cloud Save files into the local save folder.
            await Cloud.SyncOnLaunchAsync(saveFolder);

            // On quit: upload the files that changed, relative to the same folder.
            CloudSyncResult uploaded = await Cloud.SyncOnQuitAsync(saveFolder, "saves/slot1.sav");
            #endregion cloud-save

            Assert.Contains("saves/slot1.sav", uploaded.TransferredPaths);
        }
        finally
        {
            System.IO.Directory.Delete(saveFolder, recursive: true);
        }
    }

    [Fact]
    public async Task Friends_and_presence_snippet_runs()
    {
        CuratedClient.InitForTests(AppId, new RecordingTransport());

        #region friends-presence
        foreach (Friend friend in await Friends.GetFriendsAsync())
        {
            Console.WriteLine($"{friend.Name}: {friend.PresenceStatus}");
        }

        await Presence.SetAsync("in_game", "Level 3");
        await Presence.HeartbeatAsync();
        #endregion friends-presence
    }

    [Fact]
    public async Task Entitlement_snippet_runs()
    {
        CuratedClient.InitForTests(AppId, new RecordingTransport((method, path, body) =>
            "[{\"app_id\":1002,\"source\":\"primary\"}]"));

        #region entitlement
        Entitlement current = await Entitlements.CheckAsync();
        if (!current.IsEntitled)
        {
            // Refunded or revoked during the session.
        }
        #endregion entitlement

        Assert.True(current.IsEntitled);
    }

    [Fact]
    public async Task Purchase_snippet_runs()
    {
        var transport = new RecordingTransport();
        CuratedClient.InitForTests(AppId, transport);
        using var overlay = new TcpListener(IPAddress.Loopback, 0);
        overlay.Start();
        var port = ((IPEndPoint)overlay.LocalEndpoint).Port;
        Environment.SetEnvironmentVariable(LaunchContext.OverlayEndpointVariable, "127.0.0.1:" + port);
        Environment.SetEnvironmentVariable(LaunchContext.OverlayTokenVariable, "overlay-token");
        var accepted = Task.Run(async () =>
        {
            using var client = await overlay.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            var buffer = new byte[1024];
            var text = "";
            while (!text.Contains("\n"))
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length);
                text += Encoding.UTF8.GetString(buffer, 0, read);
            }

            var bytes = Encoding.UTF8.GetBytes(
                "{\"v\":1,\"type\":\"microtxn_purchase_result\",\"authorized\":true,\"status\":\"Authorized\",\"order_id\":938473,\"trans_id\":374839,\"error_code\":null,\"error_description\":null}\n");
            await stream.WriteAsync(bytes, 0, bytes.Length);
        });

        #region microtxn
        // StartCuratedIAP already called InitTxn and returned these ids as strings.
        // Parse them, then ask the Curated launcher overlay (CURATED_OVERLAY_ENDPOINT).
        // The overlay returns Authorized, Denied, or Error.
        // Outside the launcher this is Error. The client does not POST and never sees the publisher key.
        long orderId = 938473;
        long transId = 374839;
        MicroTxnAuthorization purchase = await MicroTxn.AuthorizeAsync(orderId, transId);
        switch (purchase.Status)
        {
            case MicroTxnAuthorizationStatus.Authorized:
                // Call FinalizeCuratedIAP with the order id string. The server calls FinalizeTxn,
                // then grants the catalogue qty. Do not grant from the client.
                break;
            case MicroTxnAuthorizationStatus.Denied:
                break;
            case MicroTxnAuthorizationStatus.Error:
                Console.WriteLine($"{purchase.ErrorCode} {purchase.ErrorDescription}");
                break;
        }
        #endregion microtxn

        await accepted;
        overlay.Stop();
        Assert.Equal(MicroTxnAuthorizationStatus.Authorized, purchase.Status);
        Assert.DoesNotContain(transport.Calls, call => call.RelativePath.Contains("AuthorizeTxn"));
    }

    [Fact]
    public async Task OpenId_snippet_runs()
    {
        CuratedClient.InitForTests(AppId, new RecordingTransport());

        #region openid
        // Pass it only to your own OpenID Connect consumer. Never log it.
        string idToken = await CuratedClient.GetOpenIdIdTokenAsync();
        // Unity: await AuthenticationService.Instance.SignInWithOpenIdConnectAsync("oidc-curated", idToken);
        #endregion openid

        Assert.False(string.IsNullOrEmpty(idToken));
    }

    [Fact]
    public void Game_language_snippet_runs()
    {
        #region game-language
        string language = CuratedClient.GetCurrentGameLanguage();        // "en", "es", "it", "pt-br", ...
        string steamName = CuratedClient.GetCurrentGameLanguageSteamApiName(); // "english", "spanish", ...
        #endregion game-language

        #region steam-names
        string? curated = GameLanguages.FromSteamApiName("latam");   // "es-419"
        string? steam = GameLanguages.ToSteamApiName("pt-br");       // "brazilian"
        #endregion steam-names

        Assert.False(string.IsNullOrEmpty(language));
        Assert.False(string.IsNullOrEmpty(steamName));
        Assert.Equal("es-419", curated);
        Assert.Equal("brazilian", steam);
    }

    [Fact]
    public void Shutdown_snippet_runs()
    {
        CuratedClient.InitForTests(AppId, new RecordingTransport());

        #region shutdown
        CuratedClient.Shutdown(); // on quit
        #endregion shutdown

        Assert.False(CuratedClient.IsValid);
    }
}
