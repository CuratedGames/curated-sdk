using System;
using System.Diagnostics;
using System.IO;

namespace Curated.Sdk;

/// <summary>
/// Account session already signed in on this machine. The token is for the Curated API only
/// and must not be logged.
/// </summary>
public static class LocalAccountSession
{
    /// <summary>
    /// Token from the running Curated launcher's remembered session
    /// (<c>Config/session.json</c> beside <c>Curated.exe</c>), or from the content-builder
    /// credentials file. Null when neither is present.
    /// </summary>
    public static string? TryReadToken()
    {
        var launcherToken = ReadRunningLauncherToken();
        if (!string.IsNullOrEmpty(launcherToken))
        {
            return launcherToken;
        }

        return ReadCredentialsToken();
    }

    internal static string? ReadJsonString(string? json, string key)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        var needle = "\"" + key + "\"";
        var keyIndex = json.IndexOf(needle, StringComparison.Ordinal);
        if (keyIndex < 0)
        {
            return null;
        }

        var colon = json.IndexOf(':', keyIndex + needle.Length);
        if (colon < 0)
        {
            return null;
        }

        var open = json.IndexOf('"', colon + 1);
        if (open < 0)
        {
            return null;
        }

        var close = json.IndexOf('"', open + 1);
        if (close < 0)
        {
            return null;
        }

        var value = json.Substring(open + 1, close - open - 1).Trim();
        return value.Length == 0 ? null : value;
    }

    private static string? ReadRunningLauncherToken()
    {
        var fromCurated = ReadProcessSession("Curated");
        if (!string.IsNullOrEmpty(fromCurated))
        {
            return fromCurated;
        }

        return ReadProcessSession("curated-launcher");
    }

    private static string? ReadProcessSession(string processName)
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception)
        {
            return null;
        }

        try
        {
            foreach (var process in processes)
            {
                try
                {
                    var exePath = process.MainModule?.FileName;
                    var directory = string.IsNullOrEmpty(exePath) ? null : Path.GetDirectoryName(exePath);
                    if (string.IsNullOrEmpty(directory))
                    {
                        continue;
                    }

                    var token = ReadTokenField(Path.Combine(directory, "Config", "session.json"));
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }

        return null;
    }

    private static string? ReadCredentialsToken()
    {
        var overridePath = Environment.GetEnvironmentVariable("CURATED_CREDENTIALS_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return ReadTokenField(overridePath.Trim());
        }

        var home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrEmpty(home))
        {
            home = Environment.GetEnvironmentVariable("HOME");
        }

        if (string.IsNullOrEmpty(home))
        {
            return null;
        }

        return ReadTokenField(Path.Combine(home, ".curated", "credentials.json"));
    }

    private static string? ReadTokenField(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return ReadJsonString(File.ReadAllText(path), "token");
        }
        catch (Exception)
        {
            return null;
        }
    }
}
