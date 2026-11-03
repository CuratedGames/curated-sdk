using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Curated.Unity.Editor
{
    /// <summary>
    /// Tells the running launcher where the Game view is, in screen pixels, while the Editor is
    /// playing. Unity 6 has no internal web view, so the launcher draws its own overlay window on
    /// that rectangle, the same way it covers a launched game. Leaving play mode closes the session
    /// so a purchase that play session started is cancelled. A player build does not include this file.
    /// </summary>
    [InitializeOnLoad]
    static class CuratedGameViewPanel
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static bool loggedMissing;
        static bool loggedRect;
        static int lastX;
        static int lastY;
        static int lastWidth;
        static int lastHeight;
        static bool hasLast;

        static CuratedGameViewPanel()
        {
            EditorApplication.update += Sync;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            hasLast = false;
            CuratedEditorOverlay.ClosePlaySession();
        }

        static void Sync()
        {
            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(CuratedEditorOverlay.CurrentUrl))
            {
                hasLast = false;
                return;
            }

            if (!TryGameViewScreenRect(out var x, out var y, out var width, out var height))
            {
                return;
            }

            if (hasLast && x == lastX && y == lastY && width == lastWidth && height == lastHeight)
            {
                return;
            }

            lastX = x;
            lastY = y;
            lastWidth = width;
            lastHeight = height;
            hasLast = true;
            if (!loggedRect)
            {
                loggedRect = true;
                Debug.Log("[Curated] Game view overlay " + width + "x" + height + " at " + x + "," + y);
            }

            CuratedEditorOverlay.ReportGameView(x, y, width, height);
        }

        static bool TryGameViewScreenRect(out int x, out int y, out int width, out int height)
        {
            x = 0;
            y = 0;
            width = 0;
            height = 0;
            var gameView = FindGameView();
            if (gameView == null || !IsVisibleTab(gameView))
            {
                return false;
            }

            // Unity 6 GUIView.nativeHandle is a native object pointer, not a window handle.
            // EditorWindow.position is the Game view in points. The rendered game starts under its toolbar.
            var screen = gameView.position;
            if (screen.width < 2f || screen.height < 2f)
            {
                screen = ReadRect(ParentOf(gameView), "screenPosition");
            }

            if (screen.width < 2f || screen.height < 2f)
            {
                LogMissing();
                return false;
            }

            var scale = PixelsPerPoint();
            var inset = (int)(ToolbarHeight() * scale);
            return ApplyInset(
                (int)(screen.x * scale),
                (int)(screen.y * scale),
                (int)(screen.width * scale),
                (int)(screen.height * scale),
                inset,
                out x,
                out y,
                out width,
                out height);
        }

        static bool ApplyInset(int originX, int originY, int fullWidth, int fullHeight, int inset, out int x, out int y, out int width, out int height)
        {
            if (inset > 0 && inset * 2 >= fullHeight)
            {
                inset = 0;
            }

            x = originX;
            y = originY + inset;
            width = fullWidth;
            height = fullHeight - inset;
            if (width < 2 || height < 2)
            {
                LogMissing();
                return false;
            }

            return true;
        }

        static EditorWindow FindGameView()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (type == null)
            {
                return null;
            }

            var windows = Resources.FindObjectsOfTypeAll(type);
            if (windows == null || windows.Length == 0)
            {
                return null;
            }

            EditorWindow best = null;
            var bestArea = 0f;
            foreach (var candidate in windows)
            {
                var view = candidate as EditorWindow;
                if (view == null || !IsVisibleTab(view))
                {
                    continue;
                }

                var area = view.position.width * view.position.height;
                if (area > bestArea)
                {
                    best = view;
                    bestArea = area;
                }
            }

            return best;
        }

        static bool IsVisibleTab(EditorWindow view)
        {
            var parent = ParentOf(view);
            if (parent == null)
            {
                return true;
            }

            var property = parent.GetType().GetProperty("actualView", Flags);
            if (property == null)
            {
                return true;
            }

            try
            {
                var actual = property.GetValue(parent, null) as EditorWindow;
                return actual == null || actual == view;
            }
            catch (Exception)
            {
                return true;
            }
        }

        static Rect ReadRect(object target, string propertyName)
        {
            if (target == null)
            {
                return new Rect();
            }

            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var property = type.GetProperty(propertyName, Flags | BindingFlags.DeclaredOnly);
                if (property == null || property.PropertyType != typeof(Rect))
                {
                    continue;
                }

                try
                {
                    if (property.GetValue(target, null) is Rect rect)
                    {
                        return rect;
                    }
                }
                catch (Exception)
                {
                    return new Rect();
                }
            }

            return new Rect();
        }

        static object ParentOf(EditorWindow view)
        {
            var field = typeof(EditorWindow).GetField("m_Parent", Flags);
            return field == null ? null : field.GetValue(view);
        }

        static float PixelsPerPoint()
        {
            try
            {
                var scale = EditorGUIUtility.pixelsPerPoint;
                if (scale > 0.5f)
                {
                    return scale;
                }
            }
            catch (Exception)
            {
                // The editor skin is not ready yet.
            }

            return 1f;
        }

        static float ToolbarHeight()
        {
            try
            {
                var height = EditorStyles.toolbar.fixedHeight;
                if (height > 1f)
                {
                    return height;
                }
            }
            catch (Exception)
            {
                // The editor skin is not ready yet.
            }

            return 21f;
        }

        static void LogMissing()
        {
            if (loggedMissing)
            {
                return;
            }

            loggedMissing = true;
            Debug.LogWarning("[Curated] The Game view window could not be measured, so the launcher overlay cannot cover it.");
        }
    }
}
