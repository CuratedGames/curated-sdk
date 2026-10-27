using System;
using System.Collections.Generic;

namespace Curated.Sdk;

/// <summary>A Curated game language: canonical code and optional Steam client API name (for Migrating from Steam).</summary>
public sealed class GameLanguage
{
    internal GameLanguage(string code, string englishName, string steamApiName, params string[] aliases)
    {
        Code = code;
        EnglishName = englishName;
        SteamApiName = steamApiName;
        Aliases = aliases;
    }

    /// <summary>Canonical lowercase Curated code, for example <c>en</c>, <c>it</c>, <c>pt-br</c>.</summary>
    public string Code { get; }

    /// <summary>English name, for example <c>Portuguese-Brazil</c>.</summary>
    public string EnglishName { get; }

    /// <summary>Migrating from Steam: Steam client API language name (<c>english</c>, <c>brazilian</c>, <c>koreana</c>).</summary>
    public string SteamApiName { get; }

    /// <summary>Other codes accepted for this language (for example <c>zh</c> for <c>zh-cn</c>).</summary>
    public IReadOnlyList<string> Aliases { get; }
}

/// <summary>
/// The languages <see cref="CuratedClient.GetCurrentGameLanguage"/> can return: the Full Platform
/// languages of the Curated language catalog, the same codes depots use. <see cref="ToSteamApiName"/>
/// / <see cref="FromSteamApiName"/> help when migrating from Steam.
/// </summary>
public static class GameLanguages
{
    /// <summary>Returned when the language is unknown or the game was not started by the launcher.</summary>
    public const string DefaultLanguage = "en";

    private static readonly GameLanguage[] Table =
    {
        new("ar", "Arabic", "arabic"),
        new("bg", "Bulgarian", "bulgarian"),
        new("zh-cn", "Chinese (Simplified)", "schinese", "zh", "zh-hans"),
        new("zh-tw", "Chinese (Traditional)", "tchinese", "zh-hant"),
        new("cs", "Czech", "czech"),
        new("da", "Danish", "danish"),
        new("nl", "Dutch", "dutch"),
        new("en", "English", "english"),
        new("fi", "Finnish", "finnish"),
        new("fr", "French", "french"),
        new("de", "German", "german"),
        new("el", "Greek", "greek"),
        new("hu", "Hungarian", "hungarian"),
        new("id", "Indonesian", "indonesian"),
        new("it", "Italian", "italian"),
        new("ja", "Japanese", "japanese"),
        new("ko", "Korean", "koreana"),
        new("ms", "Malay", "malay"),
        new("no", "Norwegian", "norwegian", "nb"),
        new("pl", "Polish", "polish"),
        new("pt", "Portuguese", "portuguese"),
        new("pt-br", "Portuguese-Brazil", "brazilian"),
        new("ro", "Romanian", "romanian"),
        new("ru", "Russian", "russian"),
        new("es-419", "Spanish-Latin America", "latam"),
        new("es", "Spanish-Spain", "spanish"),
        new("sv", "Swedish", "swedish"),
        new("th", "Thai", "thai"),
        new("tr", "Turkish", "turkish"),
        new("uk", "Ukrainian", "ukrainian"),
        new("vi", "Vietnamese", "vietnamese"),
    };

    private static readonly Dictionary<string, GameLanguage> ByCode = Index(includeSteamNames: false);
    private static readonly Dictionary<string, GameLanguage> ByAnyName = Index(includeSteamNames: true);

    /// <summary>Every supported game language, in catalog order.</summary>
    public static IReadOnlyList<GameLanguage> All => Table;

    /// <summary>
    /// Canonical Curated code for <paramref name="language"/>, which may be a Curated code, an alias,
    /// a BCP-47 tag (<c>pt-BR</c>, <c>it_IT</c>) or a Steam client API name (<c>italian</c>).
    /// Returns null when it is not a supported game language.
    /// </summary>
    public static string? Normalize(string? language)
    {
        var key = Clean(language);
        if (key is null)
        {
            return null;
        }

        if (ByAnyName.TryGetValue(key, out var hit))
        {
            return hit.Code;
        }

        var dash = key.IndexOf('-');
        if (dash > 0 && ByCode.TryGetValue(key.Substring(0, dash), out var primary))
        {
            return primary.Code;
        }

        return null;
    }

    /// <summary>
    /// Migrating from Steam: Steam client API language name (for example <c>english</c>,
    /// <c>brazilian</c>) for a Curated code or any value <see cref="Normalize"/> accepts. Returns
    /// null when the language is not supported.
    /// </summary>
    public static string? ToSteamApiName(string? language)
    {
        var code = Normalize(language);
        return code is null ? null : ByCode[code].SteamApiName;
    }

    /// <summary>Migrating from Steam: Curated code for a Steam client API language name (for example <c>latam</c> to <c>es-419</c>), or null.</summary>
    public static string? FromSteamApiName(string? steamApiName)
    {
        var key = Clean(steamApiName);
        if (key is null)
        {
            return null;
        }

        foreach (var language in Table)
        {
            if (language.SteamApiName == key)
            {
                return language.Code;
            }
        }

        return null;
    }

    private static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim().ToLowerInvariant().Replace('_', '-');
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static Dictionary<string, GameLanguage> Index(bool includeSteamNames)
    {
        var map = new Dictionary<string, GameLanguage>(StringComparer.Ordinal);
        foreach (var language in Table)
        {
            map[language.Code] = language;
            foreach (var alias in language.Aliases)
            {
                map[alias] = language;
            }
        }

        if (includeSteamNames)
        {
            foreach (var language in Table)
            {
                if (!map.ContainsKey(language.SteamApiName))
                {
                    map[language.SteamApiName] = language;
                }
            }
        }

        return map;
    }
}
