// Facepunch-style entry point: Init once, then use Achievements / Cloud / Friends.

using System;
using Curated.Sdk.Internal;

namespace Curated.Sdk;

/// <summary>
/// Initialize Curated once at game start, then call <see cref="Shutdown"/> on quit.
/// </summary>
public static class CuratedClient
{
    private static readonly object Gate = new();
    private static ClientState? _state;

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

        ArgumentNullException.ThrowIfNull(hubUrl);

        lock (Gate)
        {
            if (_state is not null)
            {
                throw new InvalidOperationException("CuratedClient.Init was already called");
            }

            var transport = new HttpTransport(hubUrl, sessionToken.Trim());
            var blobs = new HttpBlobTransfer();
            _state = new ClientState(appId, transport, blobs, ownsTransport: true, ownsBlobs: true);
        }
    }

    /// <summary>Shut down Curated and release network resources. Safe to call more than once.</summary>
    public static void Shutdown()
    {
        ClientState? old;
        lock (Gate)
        {
            old = _state;
            _state = null;
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

        ArgumentNullException.ThrowIfNull(transport);

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
