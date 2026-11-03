using System;
using System.Runtime.CompilerServices;
using System.Threading;
using Curated.Sdk.Internal;

[assembly: InternalsVisibleTo("Curated.Unity.Editor")]

namespace Curated.Unity
{
#if UNITY_EDITOR
    /// <summary>
    /// Remembers that the Editor attached to the launcher. The Editor assembly reports the Game view
    /// rectangle so the launcher can draw its overlay window there. The URL contains the overlay
    /// token in its fragment and must not be logged.
    /// </summary>
    internal static class CuratedEditorOverlay
    {
        static SynchronizationContext mainContext;

        internal static event Action<string> UrlReady;

        internal static string CurrentUrl { get; private set; }

        internal static void CaptureMainThread()
        {
            if (mainContext == null)
            {
                mainContext = SynchronizationContext.Current;
            }
        }

        internal static void Show(string overlayUrl)
        {
            if (string.IsNullOrWhiteSpace(overlayUrl))
            {
                return;
            }

            CurrentUrl = overlayUrl.Trim();
            void Publish()
            {
                UrlReady?.Invoke(CurrentUrl);
            }

            if (mainContext != null && SynchronizationContext.Current != mainContext)
            {
                mainContext.Post(_ => Publish(), null);
                return;
            }

            Publish();
        }

        internal static void Clear()
        {
            CurrentUrl = null;
        }

        internal static void ReportGameView(int x, int y, int width, int height)
        {
            OverlayNotifier.NotifyGameView(x, y, width, height);
        }

        /// <summary>
        /// Play mode is ending. Tell the launcher to drop this session and cancel a purchase it started.
        /// </summary>
        internal static void ClosePlaySession()
        {
            OverlayNotifier.NotifyGameClosed();
            Clear();
        }
    }
#endif
}
