using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk;
using UnityEngine;

namespace Curated.Unity
{
    /// <summary>Unity entry points for the Curated launch contract.</summary>
    public static class CuratedUnity
    {
        static bool quitHookRegistered;

        /// <summary>
        /// Verify the Curated launcher session and ownership of <paramref name="appId"/>, and shut
        /// Curated down automatically when the application quits.
        /// </summary>
        /// <exception cref="CuratedLaunchException">Contract error code in <see cref="CuratedLaunchException.Code"/>.</exception>
        public static async Task<CuratedSession> InitFromLauncherAsync(long appId, CancellationToken cancellationToken = default)
        {
            try
            {
                var session = await CuratedClient.InitFromLauncherAsync(appId, cancellationToken);
                RegisterQuitHook();
                return session;
            }
            catch (CuratedLaunchException exception) when (exception.Error == CuratedLaunchError.NotLaunchedByCurated)
            {
#if UNITY_EDITOR
                var token = LocalAccountSession.TryReadToken();
                if (!string.IsNullOrEmpty(token))
                {
                    var session = await CuratedClient.InitWithAccountSessionAsync(appId, token, cancellationToken);
                    RegisterQuitHook();
                    return session;
                }
#endif
                throw;
            }
        }

        /// <summary>
        /// Same checks as <see cref="InitFromLauncherAsync(long, CancellationToken)"/> for an account
        /// session read by the Editor. The token is sent only to the Curated API.
        /// </summary>
        public static async Task<CuratedSession> InitWithAccountSessionAsync(
            long appId,
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
            var session = await CuratedClient.InitWithAccountSessionAsync(appId, sessionToken, cancellationToken);
            RegisterQuitHook();
            return session;
        }

        static void RegisterQuitHook()
        {
            if (!quitHookRegistered)
            {
                quitHookRegistered = true;
                Application.quitting += CuratedClient.Shutdown;
            }
        }

        /// <summary>
        /// Mint a short-lived OpenID Connect ID token for the initialized session. Pass it only to
        /// Unity Authentication (<c>SignInWithOpenIdConnectAsync</c> /
        /// <c>LinkWithOpenIdConnectAsync</c> with provider <c>oidc-curated</c>). Never log it.
        /// Same as <see cref="CuratedClient.GetOpenIdIdTokenAsync"/>.
        /// </summary>
        /// <exception cref="CuratedLaunchException">Contract error code in <see cref="CuratedLaunchException.Code"/>.</exception>
        public static Task<string> GetOpenIdIdTokenAsync(CancellationToken cancellationToken = default) =>
            CuratedClient.GetOpenIdIdTokenAsync(cancellationToken);

        /// <summary>
        /// Game language the player selected in the Curated launcher (<c>en</c>, <c>es</c>, <c>it</c>, ...),
        /// or <c>en</c> outside the launcher. Same as <see cref="CuratedClient.GetCurrentGameLanguage"/>.
        /// </summary>
        public static string GetCurrentGameLanguage() => CuratedClient.GetCurrentGameLanguage();

        /// <summary>Migrating from Steam: Steam client language API name for <see cref="GetCurrentGameLanguage"/> (<c>english</c>, <c>spanish</c>, ...).</summary>
        public static string GetCurrentGameLanguageSteamApiName() => CuratedClient.GetCurrentGameLanguageSteamApiName();
    }
}
