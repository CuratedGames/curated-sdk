# Game language

Every binding returns the language the player selected in the Curated launcher. It is the
launcher's client language, the same language the launcher uses to download
[language depots](/admin/game-build/depots#depot-localization). It comes back as a lowercase
Curated code (`en`, `es`, `it`, `pt-br`, ...), and `en` outside the launcher. It works before the
launch check.

| Binding | Call |
| --- | --- |
| .NET / Unity | `CuratedClient.GetCurrentGameLanguage()` / `CuratedUnity.GetCurrentGameLanguage()` |
| Godot | `Curated.get_current_game_language()` |
| Unreal | `UCuratedSubsystem::GetCurrentGameLanguage()` (Blueprint: **Get Current Game Language**) |
| RPG Maker | `Curated.getCurrentGameLanguage()` |

<<< @/../Curated.Sdk.Tests/DocsSnippetsTests.cs#game-language{csharp}

Examples per engine:

- Godot: `TranslationServer.set_locale(Curated.get_current_game_language())`
- Unreal: `FInternationalization::Get().SetCurrentCulture(UCuratedSubsystem::GetCurrentGameLanguage())`
- Unity Localization: pick the `Locale` whose code matches.

The full list of codes is on [Game languages](/reference/languages). Porting from Steam? See
[Migrating from Steam](./migrating-from-steam).
