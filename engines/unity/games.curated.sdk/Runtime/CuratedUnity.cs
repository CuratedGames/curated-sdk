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
    }
}
