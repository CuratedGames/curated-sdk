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
            var session = await CuratedClient.InitFromLauncherAsync(appId, cancellationToken);
            if (!quitHookRegistered)
            {
                quitHookRegistered = true;
                Application.quitting += CuratedClient.Shutdown;
            }

            return session;
        }

        /// <summary>
        /// Game language the player selected in the Curated launcher (<c>en</c>, <c>es</c>, <c>it</c>, ...),
        /// or <c>en</c> outside the launcher. Same as <see cref="CuratedClient.GetCurrentGameLanguage"/>.
        /// </summary>
        public static string GetCurrentGameLanguage() => CuratedClient.GetCurrentGameLanguage();

        /// <summary>Steam API language name for <see cref="GetCurrentGameLanguage"/> (<c>english</c>, <c>spanish</c>, ...).</summary>
        public static string GetCurrentGameLanguageSteamApiName() => CuratedClient.GetCurrentGameLanguageSteamApiName();
    }
}
