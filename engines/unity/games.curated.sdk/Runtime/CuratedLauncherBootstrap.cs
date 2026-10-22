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

        [Tooltip("Skip the launcher check in the Unity Editor so you can play without the Curated launcher.")]
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
