using System;
using System.Threading;
using Curated.Sdk;
using UnityEngine;
using UnityEngine.Events;

namespace Curated.Unity
{
    /// <summary>
    /// Drop on a GameObject in the first scene to run the Curated launch check on startup.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CuratedLauncherBootstrap : MonoBehaviour
    {
        [Serializable]
        public sealed class FailedEvent : UnityEvent<string>
        {
        }

        [Tooltip("This game's Curated app id (from the Curated developer admin).")]
        [SerializeField]
        long appId;

        [Tooltip("In the Editor, connect to a running Curated launcher when possible, and keep playing if that connection fails.")]
        [SerializeField]
        bool skipInEditor = true;

        [Tooltip("Quit the application when the check fails (not started by Curated, not owned, or expired session).")]
        [SerializeField]
        bool quitOnFailure = true;

        [Tooltip("Invoked once the player is signed in and owns the game. Read CuratedClient.Session for player details.")]
        [SerializeField]
        UnityEvent onReady = new UnityEvent();

        [Tooltip("Invoked with the launch contract error code (for example not_entitled) when the check fails.")]
        [SerializeField]
        FailedEvent onFailed = new FailedEvent();

        readonly CancellationTokenSource destroyed = new CancellationTokenSource();

        async void Start()
        {
            if (Application.isEditor && skipInEditor)
            {
                if (appId > 0)
                {
                    try
                    {
                        await CuratedUnity.InitFromLauncherAsync(appId, destroyed.Token);
                    }
                    catch (CuratedLaunchException e)
                    {
                        Debug.LogWarning($"[Curated] Editor play without a launcher session: {e.Code}");
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }

                onReady.Invoke();
                return;
            }

            try
            {
                await CuratedUnity.InitFromLauncherAsync(appId, destroyed.Token);
                onReady.Invoke();
            }
            catch (CuratedLaunchException e)
            {
                Debug.LogWarning($"[Curated] Launch check failed: {e.Code}");
                onFailed.Invoke(e.Code);
                if (quitOnFailure)
                {
                    Application.Quit();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        void OnDestroy()
        {
            destroyed.Cancel();
            destroyed.Dispose();
        }
    }
}
