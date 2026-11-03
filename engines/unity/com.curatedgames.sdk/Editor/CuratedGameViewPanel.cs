using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Curated.Unity.Editor
{
    /// <summary>
    /// Loads the launcher overlay page in a panel that tracks the Game view while the Editor
    /// is playing. That page is the purchase and achievement UI. The URL holds the overlay token
    /// in its fragment and is never logged. Only a loopback http(s) URL is loaded.
    /// A player build does not include this file.
    /// </summary>
    [InitializeOnLoad]
    static class CuratedGameViewPanel
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static CuratedOverlayWindow window;
        static bool loggedMissing;
        static bool loggedInitFailed;
        static bool pageReady;

        static CuratedGameViewPanel()
        {
            CuratedEditorOverlay.UrlReady += _ => Sync();
            EditorApplication.update += Sync;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                CuratedEditorOverlay.Clear();
                pageReady = false;
                CloseWindow();
            }
        }

        static void Sync()
        {
            var url = CuratedEditorOverlay.CurrentUrl;
            if (!EditorApplication.isPlaying || string.IsNullOrEmpty(url) || !IsLoopbackHttp(url))
            {
                if (!EditorApplication.isPlaying)
                {
                    pageReady = false;
                    CloseWindow();
                }

                return;
            }

            if (WebViewType() == null)
            {
                return;
            }

            var gameView = FindGameView();
            if (gameView == null || !IsVisibleTab(gameView))
            {
                pageReady = false;
                CloseWindow();
                return;
            }

            var rect = ContentScreenRect(gameView);
            if (rect.width < 2f || rect.height < 2f)
            {
                return;
            }

            if (window == null)
            {
                window = ScriptableObject.CreateInstance<CuratedOverlayWindow>();
                window.titleContent = new GUIContent("Curated");
                window.ShowUtility();
                window.position = rect;
                pageReady = false;
            }
            else if (window.position != rect)
            {
                window.position = rect;
            }

            if (!pageReady || window.position != rect)
            {
                window.Repaint();
            }
        }

        static void CloseWindow()
        {
            if (window == null)
            {
                return;
            }

            var open = window;
            window = null;
            open.Close();
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

        static Rect ContentScreenRect(EditorWindow gameView)
        {
            var parent = ParentOf(gameView);
            var screen = ReadRect(parent, "screenPosition");
            if (screen.width < 2f || screen.height < 2f)
            {
                screen = gameView.position;
            }

            var chrome = ToolbarHeight();
            // Dock tabs, then the Game view toolbar, sit above the rendered game.
            screen.y += chrome * 2f;
            screen.height = Mathf.Max(1f, screen.height - chrome * 2f);
            return screen;
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

        static Type webViewType;
        static bool webViewTypeSearched;

        internal static Type WebViewType()
        {
            if (webViewTypeSearched)
            {
                return webViewType;
            }

            webViewTypeSearched = true;
            var editor = typeof(UnityEditor.Editor).Assembly;
            webViewType = editor.GetType("UnityEditor.WebView") ?? editor.GetType("UnityEditor.Web.WebView");
            if (webViewType == null)
            {
                LogMissing();
            }

            return webViewType;
        }

        static void LogMissing()
        {
            if (loggedMissing)
            {
                return;
            }

            loggedMissing = true;
            Debug.LogWarning(
                "[Curated] This Unity editor has no internal web view, so the launcher overlay page cannot be shown over the Game view.");
        }

        static void LogInitFailed()
        {
            if (loggedInitFailed)
            {
                return;
            }

            loggedInitFailed = true;
            Debug.LogWarning(
                "[Curated] The editor web view did not attach to the Game view panel, so the launcher overlay page is not visible.");
        }

        static bool IsLoopbackHttp(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }

            if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return System.Net.IPAddress.TryParse(uri.Host, out var address) && System.Net.IPAddress.IsLoopback(address);
        }

        sealed class CuratedOverlayWindow : EditorWindow
        {
            ScriptableObject webView;
            string loadedUrl;

            void OnGUI()
            {
                var url = CuratedEditorOverlay.CurrentUrl;
                if (string.IsNullOrEmpty(url) || !IsLoopbackHttp(url))
                {
                    return;
                }

                if (Event.current.type != EventType.Repaint && webView != null)
                {
                    return;
                }

                var parent = ParentOf(this);
                if (parent == null || position.width < 2f || position.height < 2f)
                {
                    return;
                }

                var type = WebViewType();
                if (type == null)
                {
                    return;
                }

                var width = Mathf.Max(1, (int)position.width);
                var height = Mathf.Max(1, (int)position.height);
                if (webView == null)
                {
                    if (Event.current.type != EventType.Repaint)
                    {
                        return;
                    }

                    var created = ScriptableObject.CreateInstance(type);
                    if (!Call(created, "InitWebView", parent, 0, 0, width, height, false)
                        && !Call(created, "InitWebView", parent, new Rect(0f, 0f, width, height), false, 0)
                        && !Call(created, "InitWebView", parent, new Rect(0f, 0f, width, height), false))
                    {
                        DestroyImmediate(created);
                        LogInitFailed();
                        return;
                    }

                    webView = created;
                    loadedUrl = null;
                    pageReady = false;
                }

                Call(webView, "SetHostView", parent);
                Call(webView, "SetSizeAndPosition", 0, 0, width, height);
                Call(webView, "Show");
                if (string.Equals(loadedUrl, url, StringComparison.Ordinal))
                {
                    pageReady = true;
                    return;
                }

                if (!Call(webView, "LoadURL", url) && !Call(webView, "LoadURL", url, false) && !Call(webView, "LoadUrl", url))
                {
                    return;
                }

                loadedUrl = url;
                pageReady = true;
                Call(webView, "Show");
                Call(webView, "SetFocus", true);
            }

            void OnDestroy()
            {
                pageReady = false;
                if (webView != null)
                {
                    Call(webView, "Hide");
                    DestroyImmediate(webView);
                    webView = null;
                }

                if (window == this)
                {
                    window = null;
                }
            }
        }

        static System.Collections.Generic.IEnumerable<MethodInfo> Methods(Type type)
        {
            while (type != null)
            {
                foreach (var method in type.GetMethods(Flags | BindingFlags.DeclaredOnly))
                {
                    yield return method;
                }

                type = type.BaseType;
            }
        }

        static bool Call(object target, string name, params object[] args)
        {
            if (target == null)
            {
                return false;
            }

            foreach (var method in Methods(target.GetType()))
            {
                if (!string.Equals(method.Name, name, StringComparison.Ordinal))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != args.Length)
                {
                    continue;
                }

                var matches = true;
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (args[i] == null)
                    {
                        if (parameters[i].ParameterType.IsValueType)
                        {
                            matches = false;
                            break;
                        }

                        continue;
                    }

                    var parameterType = parameters[i].ParameterType;
                    var argumentType = args[i].GetType();
                    if (parameterType != argumentType && !parameterType.IsAssignableFrom(argumentType))
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                try
                {
                    method.Invoke(target, args);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }
    }
}
