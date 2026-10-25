// Entry point: Init once, then use Achievements / Cloud / Friends.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>
/// Initialize Curated once at game start, then call <see cref="Shutdown"/> on quit.
/// </summary>
public static class CuratedClient
{
    private static readonly object Gate = new();
    private static ClientState? _state;
    private static CuratedSession? _session;

    /// <summary>Default Curated hub used when Init is called without an explicit URL.</summary>
    public static readonly Uri DefaultHubUrl = new("https://api.curated.games");

    /// <summary>True after a successful <see cref="Init(long,string)"/>.</summary>
    public static bool IsValid
    {
        get
        {
            lock (Gate)
            {
                return _state is not null;
            }
        }
    }

    /// <summary>App id passed to Init.</summary>
    public static long AppId
    {
        get
        {
            lock (Gate)
            {
                EnsureInitialized_NoLock();
                return _state!.AppId;
            }
        }
    }

    /// <summary>
    /// Start Curated for this game. Pass the session token from the Curated launcher
    /// (or a signed-in player session). Throws if already initialized or arguments are invalid.
    /// </summary>
    public static void Init(long appId, string sessionToken) =>
        Init(appId, sessionToken, DefaultHubUrl);

    /// <summary>
    /// Start Curated with an explicit hub URL (local or staging). Prefer the overload
    /// without a URL for shipping builds.
    /// </summary>
    public static void Init(long appId, string sessionToken, Uri hubUrl)
    {
        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId), "app id must be positive");
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("session token is required", nameof(sessionToken));
        }

        Guard.NotNull(hubUrl, nameof(hubUrl));
        InitCore(appId, new HttpTransport(hubUrl, sessionToken.Trim()));
    }

    /// <summary>
    /// The authenticated, entitled session after a successful
    /// <see cref="InitFromLauncherAsync(long, CancellationToken)"/>; otherwise null.
    /// </summary>
    public static CuratedSession? Session
    {
        get
        {
            lock (Gate)
            {
                return _session;
            }
        }
    }

    /// <summary>
    /// Start Curated from the launch variables the Curated launcher set on this process,
    /// verify the player session, and confirm the player owns <paramref name="appId"/>.
    /// On failure Curated is left shut down and a <see cref="CuratedLaunchException"/> carries the
    /// launch contract error code.
    /// </summary>
    /// <param name="appId">This game's Curated app id (compiled into the game).</param>
    /// <param name="cancellationToken">Cancels the session and entitlement calls.</param>
    public static Task<CuratedSession> InitFromLauncherAsync(long appId, CancellationToken cancellationToken = default) =>
        InitFromLauncherAsync(appId, LaunchContext.FromEnvironment(appId), null, cancellationToken);

    internal static Task<CuratedSession> InitFromLauncherAsync(
        long appId,
        IReadOnlyDictionary<string, string?> environment,
        HttpMessageHandler? handler,
        CancellationToken cancellationToken = default) =>
        InitFromLauncherAsync(appId, LaunchContext.ParseVariables(environment, appId), handler, cancellationToken);

    private static async Task<CuratedSession> InitFromLauncherAsync(
        long appId,
        LaunchContext context,
        HttpMessageHandler? handler,
        CancellationToken cancellationToken)
    {
        var state = InitCore(appId, new HttpTransport(context.ApiBase, context.SessionToken, handler));
        try
        {
            var session = await SessionService.StartAsync(state, context, cancellationToken).ConfigureAwait(false);
            lock (Gate)
            {
                if (ReferenceEquals(_state, state))
                {
                    _session = session;
                }
            }

            return session;
        }
        catch
        {
            Shutdown();
            throw;
        }
    }

    private static ClientState InitCore(long appId, HttpTransport transport)
    {
        lock (Gate)
        {
            if (_state is not null)
            {
                transport.Dispose();
                throw new InvalidOperationException("CuratedClient.Init was already called");
            }

            var blobs = new HttpBlobTransfer();
            _state = new ClientState(appId, transport, blobs, ownsTransport: true, ownsBlobs: true);
            return _state;
        }
    }

    /// <summary>
    /// The game language the player selected in the Curated launcher, as a lowercase Curated code
    /// (<c>en</c>, <c>es</c>, <c>it</c>, and any other code in <see cref="GameLanguages.All"/>), like
    /// Steamworks <c>ISteamApps::GetCurrentGameLanguage</c>. It is the launcher's client language,
    /// the same one the launcher uses to pick language depots. Returns <c>en</c> when the game was
    /// not started by the launcher, the launcher did not send a language, or the language is not a
    /// Curated game language. Works before and without <see cref="InitFromLauncherAsync(long, CancellationToken)"/>.
    /// Use <see cref="GetCurrentGameLanguageSteamApiName"/> for the Steam API name.
    /// </summary>
    public static string GetCurrentGameLanguage() =>
        ResolveGameLanguage(Environment.GetEnvironmentVariable(LaunchContext.LanguageVariable));

    /// <summary>
    /// <see cref="GetCurrentGameLanguage"/> as a Steam API language name (<c>english</c>, <c>spanish</c>,
    /// <c>italian</c>, <c>brazilian</c>, ...), for code ported from Steamworks. Returns <c>english</c> as the fallback.
    /// </summary>
    public static string GetCurrentGameLanguageSteamApiName() =>
        GameLanguages.ToSteamApiName(GetCurrentGameLanguage()) ?? "english";

    internal static string ResolveGameLanguage(string? raw) =>
        GameLanguages.Normalize(raw) ?? GameLanguages.DefaultLanguage;

    /// <summary>Shut down Curated and release network resources. Safe to call more than once.</summary>
    public static void Shutdown()
    {
        ClientState? old;
        lock (Gate)
        {
            old = _state;
            _state = null;
            _session = null;
        }

        old?.Dispose();
    }

    /// <summary>Test hook: inject fakes without opening the network.</summary>
    internal static void InitForTests(long appId, ITransport transport, IBlobTransfer? blobs = null)
    {
        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId));
        }

        Guard.NotNull(transport, nameof(transport));

        // Tests always replace the prior fake client.
        Shutdown();
        lock (Gate)
        {
            _state = new ClientState(
                appId,
                transport,
                blobs ?? new MemoryBlobTransfer(),
                ownsTransport: false,
                ownsBlobs: blobs is null);
        }
    }

    internal static ClientState State
    {
        get
        {
            lock (Gate)
            {
                EnsureInitialized_NoLock();
                return _state!;
            }
        }
    }

    private static void EnsureInitialized_NoLock()
    {
        if (_state is null)
        {
            throw new InvalidOperationException("Call CuratedClient.Init before using Curated");
        }
    }

    internal sealed class ClientState : IDisposable
    {
        public ClientState(
            long appId,
            ITransport transport,
            IBlobTransfer blobs,
            bool ownsTransport,
            bool ownsBlobs)
        {
            AppId = appId;
            Transport = transport;
            Blobs = blobs;
            _ownsTransport = ownsTransport;
            _ownsBlobs = ownsBlobs;
        }

        public long AppId { get; }

        public ITransport Transport { get; }

        public IBlobTransfer Blobs { get; }

        private readonly bool _ownsTransport;
        private readonly bool _ownsBlobs;

        public void Dispose()
        {
            if (_ownsTransport && Transport is IDisposable d1)
            {
                d1.Dispose();
            }

            if (_ownsBlobs && Blobs is IDisposable d2)
            {
                d2.Dispose();
            }
        }
    }
}
