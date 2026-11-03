using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Curated.Unity.Editor
{
    /// <summary>
    /// Loads the launcher overlay page inside the Game view. That page is the purchase and
    /// achievement UI. The page URL contains the overlay token, so this type never logs it.
    /// Only loopback http(s) URLs are loaded.
    /// </summary>
    [InitializeOnLoad]
    static class CuratedGameViewPanel
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static ScriptableObject webView;
        static object host;
        static bool ready;
        static bool loggedMissing;
        static string loadedUrl;
        static Rect placed;

        static CuratedGameViewPanel()
        {
            CuratedEditorOverlay.UrlReady += _ => TryShow();
            EditorApplication.update += TryShow;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        static void OnPlayMode(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            {
                CuratedEditorOverlay.Clear();
                Hide();
            }
        }

        static void TryShow()
        {
            var url = CuratedEditorOverlay.CurrentUrl;
            if (string.IsNullOrEmpty(url) || !EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlaying)
                {
                    Hide();
                }

                return;
            }

            if (!IsLoopbackHttp(url))
            {
                return;
            }

            var gameView = FindGameView();
            if (gameView == null || !EnsureWebView(gameView))
            {
                return;
            }

            var rect = ContentRect(gameView);
            if (rect != placed)
            {
                Call(webView, "SetSizeAndPosition", (int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
                placed = rect;
            }

            if (string.Equals(loadedUrl, url, StringComparison.Ordinal))
            {
                return;
            }

            if (!Call(webView, "LoadURL", url) && !Call(webView, "LoadURL", url, false))
            {
                LogMissing();
                return;
            }

            loadedUrl = url;
            Call(webView, "Show");
            TryTransparent();
        }

        static bool EnsureWebView(EditorWindow gameView)
        {
            var parent = ParentOf(gameView);
            if (parent == null)
            {
                return false;
            }

            var type = FindWebViewType();
            if (type == null)
            {
                LogMissing();
                return false;
            }

            if (webView == null)
            {
                webView = ScriptableObject.CreateInstance(type);
                webView.hideFlags = HideFlags.HideAndDontSave;
            }

            if (ready && ReferenceEquals(host, parent))
            {
                return true;
            }

            var rect = ContentRect(gameView);
            var opened = Call(
                    webView,
                    "InitWebView",
                    parent,
                    (int)rect.x,
                    (int)rect.y,
                    (int)rect.width,
                    (int)rect.height,
                    false)
                || Call(webView, "InitWebView", parent, rect, false, 0)
                || Call(webView, "InitWebView", parent, rect, false);
            if (!opened)
            {
                LogMissing();
                return false;
            }

            host = parent;
            ready = true;
            loadedUrl = null;
            placed = rect;
            TryTransparent();
            return true;
        }

        static void TryTransparent()
        {
            if (webView == null)
            {
                return;
            }

            Call(webView, "SetTransparent", true);
            Call(webView, "SetBackgroundColor", Color.clear);
            var property = webView.GetType().GetProperty("transparent", Flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                try
                {
                    property.SetValue(webView, true);
                }
                catch (Exception)
                {
                    // A missing transparency hook still leaves the launcher page as the UI.
                }
            }
        }

        static void Hide()
        {
            if (webView != null)
            {
                Call(webView, "Hide");
                UnityEngine.Object.DestroyImmediate(webView);
                webView = null;
            }

            host = null;
            ready = false;
            loadedUrl = null;
            placed = new Rect();
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

            return windows[0] as EditorWindow;
        }

        static object ParentOf(EditorWindow window)
        {
            var field = typeof(EditorWindow).GetField("m_Parent", Flags);
            return field == null ? null : field.GetValue(window);
        }

        static Rect ContentRect(EditorWindow gameView)
        {
            var width = Mathf.Max(1f, gameView.position.width);
            var height = Mathf.Max(1f, gameView.position.height);
            const float toolbar = 20f;
            return new Rect(0f, toolbar, width, Mathf.Max(1f, height - toolbar));
        }

        static Type FindWebViewType()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var direct = assembly.GetType("UnityEditor.WebView");
            if (direct != null)
            {
                return direct;
            }

            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Name == "WebView")
                    {
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException ex)
            {
                if (ex.Types == null)
                {
                    return null;
                }

                foreach (var type in ex.Types)
                {
                    if (type != null && type.Name == "WebView")
                    {
                        return type;
                    }
                }
            }

            return null;
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
                if (method.Name != name)
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
                    var argType = args[i].GetType();
                    if (parameterType != argType && !parameterType.IsAssignableFrom(argType))
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

        static void LogMissing()
        {
            if (loggedMissing)
            {
                return;
            }

            loggedMissing = true;
            Debug.LogWarning("[Curated] The Game view could not host the launcher overlay page.");
        }
    }
}
