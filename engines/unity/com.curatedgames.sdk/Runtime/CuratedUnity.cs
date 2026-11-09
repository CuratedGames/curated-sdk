using System;
using System.Threading;
using System.Threading.Tasks;
using Curated.Sdk;
using Curated.Sdk.Internal;
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
        /// In the Unity Editor, send editor_attach to a running launcher without requiring
        /// <c>CURATED_LAUNCHER</c> or a process the launcher started. On success, the launcher draws
        /// its overlay on the Game view and this call does not throw <c>not_launched_by_curated</c>.
        /// </summary>
        /// <exception cref="CuratedLaunchException">Contract error code in <see cref="CuratedLaunchException.Code"/>.</exception>
        public static async Task<CuratedSession> InitFromLauncherAsync(long appId, CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            CuratedEditorOverlay.CaptureMainThread();
            var attach = await EditorLauncherBridge.TryAttachAsync(appId, LocalAccountSession.TryReadToken(), cancellationToken);
            if (attach != null && attach.Succeeded)
            {
                AdoptEditorOverlay(attach);
                try
                {
                    var launched = await CuratedClient.InitFromLauncherAsync(appId, cancellationToken);
                    RegisterQuitHook();
                    return launched;
                }
                catch (CuratedLaunchException exception) when (exception.Error == CuratedLaunchError.NotLaunchedByCurated)
                {
                    var session = await CuratedClient.InitWithAccountSessionAsync(appId, attach.AccountToken, cancellationToken);
                    RegisterQuitHook();
                    return session;
                }
            }

            if (attach != null && attach.NotSignedIn)
            {
                try
                {
                    var launched = await CuratedClient.InitFromLauncherAsync(appId, cancellationToken);
                    RegisterQuitHook();
                    return launched;
                }
                catch (CuratedLaunchException exception) when (exception.Error == CuratedLaunchError.NotLaunchedByCurated)
                {
                    throw;
                }
            }
#endif
            // Player builds compile only the path below. They do not send editor_attach and they
            // do not skip not_launched_by_curated. They use the launcher process: CURATED_LAUNCHER
            // and the overlay variables.
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
        /// In the Editor, editor_attach also lets the launcher draw its overlay on the Game view when
        /// the launcher accepts the signed-in account.
        /// </summary>
        public static async Task<CuratedSession> InitWithAccountSessionAsync(
            long appId,
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            CuratedEditorOverlay.CaptureMainThread();
            var accountToken = LocalAccountSession.TryReadToken();
            if (string.IsNullOrEmpty(accountToken))
            {
                accountToken = sessionToken;
            }

            var attach = await EditorLauncherBridge.TryAttachAsync(appId, accountToken, cancellationToken);
            if (attach != null && attach.Succeeded)
            {
                AdoptEditorOverlay(attach);
            }
#endif
            var session = await CuratedClient.InitWithAccountSessionAsync(appId, sessionToken, cancellationToken);
            RegisterQuitHook();
            return session;
        }

#if UNITY_EDITOR
        static void AdoptEditorOverlay(EditorLauncherBridge.EditorAttachResult attach)
        {
            EditorLauncherBridge.Apply(attach);
            CuratedEditorOverlay.Show(attach.OverlayUrl);
            Debug.Log("[Curated] Connected the Editor to the Curated launcher. Purchases use the launcher overlay on the Game view.");
        }
#endif

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

        /// <summary>
        /// Open a published Curated store page in the launcher overlay.
        /// <see cref="OverlayToStoreFlag.None"/> opens the page with no cart action.
        /// <see cref="OverlayToStoreFlag.AddToCart"/> and <see cref="OverlayToStoreFlag.AddToCartAndShow"/>
        /// also add the app to the cart, and only for a released or preorderable app.
        /// Same as <see cref="CuratedClient.OpenStoreOverlay"/>.
        /// </summary>
        /// <param name="appId">Published Curated app whose store page should open. This can be a different app than the one that is running.</param>
        /// <param name="flag">Store page only, add to cart, or add to cart and show the cart.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="appId"/> is not positive.</exception>
        /// <exception cref="InvalidOperationException">Curated is not initialized.</exception>
        /// <exception cref="CuratedLaunchException">The player does not own the running game.</exception>
        /// <exception cref="CuratedException">The overlay is unavailable, or the launcher rejected the request. <see cref="Exception.Message"/> is the English reason.</exception>
        public static void OpenStoreOverlay(long appId, OverlayToStoreFlag flag) =>
            CuratedClient.OpenStoreOverlay(appId, flag);
    }
}
