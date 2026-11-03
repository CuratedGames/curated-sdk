using System;
using System.Runtime.CompilerServices;
using System.Threading;

[assembly: InternalsVisibleTo("Curated.Unity.Editor")]

namespace Curated.Unity
{
#if UNITY_EDITOR
    /// <summary>
    /// Hands the launcher overlay page URL to the Editor assembly, which hosts it over the Game view.
    /// The URL contains the overlay token in its fragment and must not be logged.
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
    }
#endif
}
