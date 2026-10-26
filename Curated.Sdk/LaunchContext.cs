using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Curated.Sdk;

/// <summary>
/// Values the Curated launcher sets on the game process (launch contract v1).
/// Read with <see cref="FromEnvironment"/>; most games call
/// <see cref="CuratedClient.InitFromLauncherAsync(long, System.Threading.CancellationToken)"/> instead.
/// </summary>
public sealed class LaunchContext
{
    /// <summary>Newest launch contract version this SDK understands.</summary>
    public const int SupportedContractVersion = 1;

    public const string LauncherVariable = "CURATED_LAUNCHER";
    public const string ContractVersionVariable = "CURATED_CONTRACT_VERSION";
    public const string AppIdVariable = "CURATED_APP_ID";
    public const string SessionTokenVariable = "CURATED_SESSION_TOKEN";
    public const string ApiBaseVariable = "CURATED_API_BASE";
    public const string UserIdVariable = "CURATED_USER_ID";
    public const string UsernameVariable = "CURATED_USERNAME";
    public const string LanguageVariable = "CURATED_LANGUAGE";

    private LaunchContext(
        int contractVersion,
        long appId,
        string sessionToken,
        Uri apiBase,
        string? userIdHint,
        string? usernameHint,
        string? language)
    {
        ContractVersion = contractVersion;
        AppId = appId;
        SessionToken = sessionToken;
        ApiBase = apiBase;
        UserIdHint = userIdHint;
        UsernameHint = usernameHint;
        Language = language;
    }

    /// <summary>Contract version the launcher reported.</summary>
    public int ContractVersion { get; }

    /// <summary>Curated app id the player launched.</summary>
    public long AppId { get; }

    /// <summary>API origin without a trailing slash.</summary>
    public Uri ApiBase { get; }

    /// <summary>Player id hint from the launcher. The session call returns the authoritative value.</summary>
    public string? UserIdHint { get; }

    /// <summary>Player display name hint from the launcher.</summary>
    public string? UsernameHint { get; }

    /// <summary>
    /// Raw <c>CURATED_LANGUAGE</c> value: the launcher's client language (<c>en</c>, <c>es</c>, <c>it</c>), or null.
    /// <see cref="CuratedClient.GetCurrentGameLanguage"/> normalizes it and falls back to <c>en</c>.
    /// </summary>
    public string? Language { get; }

    internal string SessionToken { get; }

    /// <summary>
    /// Account session used by the Editor when this process was not started by the launcher.
    /// The token is still sent only to <paramref name="apiBase"/>.
    /// </summary>
    internal static LaunchContext ForAccountSession(long appId, string sessionToken, Uri apiBase)
    {
        if (appId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(appId), "app id must be positive");
        }

        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.NotLaunchedByCurated,
                "The game was not started by the Curated launcher.");
        }

        if (apiBase is null
            || (apiBase.Scheme != Uri.UriSchemeHttps && apiBase.Scheme != Uri.UriSchemeHttp))
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.NotLaunchedByCurated,
                "The Curated launcher passed an invalid API address.");
        }

        return new LaunchContext(
            SupportedContractVersion,
            appId,
            sessionToken.Trim(),
            apiBase,
            null,
            null,
            null);
    }

    /// <summary>True when the process looks like it was started by the Curated launcher.</summary>
    public static bool IsLaunchedByCurated() =>
        Read(Environment.GetEnvironmentVariables(), LauncherVariable) == "1";

    /// <summary>
    /// Read and validate the launch variables of the current process for a game whose Curated
    /// app id is <paramref name="expectedAppId"/>.
    /// </summary>
    /// <exception cref="CuratedLaunchException">The variables are missing or invalid.</exception>
    public static LaunchContext FromEnvironment(long expectedAppId) =>
        Parse(Environment.GetEnvironmentVariables(), expectedAppId);

    internal static LaunchContext Parse(IDictionary environment, long expectedAppId)
    {
        if (expectedAppId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedAppId), "app id must be positive");
        }

        var token = Read(environment, SessionTokenVariable);
        if (Read(environment, LauncherVariable) != "1" || token is null)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.NotLaunchedByCurated,
                "The game was not started by the Curated launcher.");
        }

        var versionRaw = Read(environment, ContractVersionVariable);
        if (!int.TryParse(versionRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
            || version <= 0
            || version > SupportedContractVersion)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.UnsupportedContractVersion,
                "The Curated launcher uses a newer launch contract than this game supports.");
        }

        var appRaw = Read(environment, AppIdVariable);
        if (!long.TryParse(appRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var appId)
            || appId != expectedAppId)
        {
            throw new CuratedLaunchException(
                CuratedLaunchError.AppMismatch,
                "The Curated launcher started this game for a different app.");
        }

        var apiBase = CuratedClient.DefaultHubUrl;
        var apiRaw = Read(environment, ApiBaseVariable);
        if (apiRaw is not null)
        {
            if (!Uri.TryCreate(apiRaw.TrimEnd('/'), UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
            {
                throw new CuratedLaunchException(
                    CuratedLaunchError.NotLaunchedByCurated,
                    "The Curated launcher passed an invalid API address.");
            }

            apiBase = parsed;
        }

        return new LaunchContext(
            version,
            appId,
            token,
            apiBase,
            Read(environment, UserIdVariable),
            Read(environment, UsernameVariable),
            Read(environment, LanguageVariable));
    }

    internal static LaunchContext ParseVariables(IReadOnlyDictionary<string, string?> environment, long expectedAppId)
    {
        var table = new Hashtable(StringComparer.Ordinal);
        foreach (var pair in environment)
        {
            table[pair.Key] = pair.Value;
        }

        return Parse(table, expectedAppId);
    }

    /// <summary>API origin as text, without a trailing slash.</summary>
    public string ApiBaseText => ApiBase.AbsoluteUri.TrimEnd('/');

    private static string? Read(IDictionary environment, string name)
    {
        var value = environment[name] as string;
        if (value is null)
        {
            return null;
        }

        value = value.Trim();
        return value.Length == 0 ? null : value;
    }
}
