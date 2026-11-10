# How Playnite 10 localises an extension and a theme

Research for [#165](https://github.com/snrgarb/xbox-series-theme/issues/165) (part of the map [#164](https://github.com/snrgarb/xbox-series-theme/issues/164)).

**Question:** what does Playnite 10 give an add-on for translated strings, and what does that fix for the **Bundle**'s translation base?

**Sources and versions checked (2026-11-11):**

- Playnite source at tag `10.64` [`e2e6efc`](https://github.com/JosefNemec/Playnite/tree/e2e6efcda311fea38fcd92f61c8ca90811d97f28) (released 2026-11-09). Read from a local checkout at `3e8abb2` (2026-10-19); GitHub's compare of that commit with `10.64` shows none of the files cited below changed, apart from the contents of some of Playnite's own language files. Links below are pinned to `10.64`, abbreviated `PN/` for `https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/`.
- The SDK this repo builds against: `PlayniteSDK` `6.18.0` (`extension/XboxSeries.Extension/XboxSeries.Extension.csproj`), read from the NuGet cache (`~/.nuget/packages/playnitesdk/6.18.0/lib/net462/Playnite.SDK.xml`).
- Playnite docs repo [`Playnite/Docs@41d6529`](https://github.com/Playnite/Docs/tree/41d6529c7b96335211777031e5a7b36b86cd982a), the source of api.playnite.link, last pushed May 2025. The code wins where they differ; here they agree.
- Microsoft's WPF docs for the resource lookup rules Playnite relies on.

Nothing here was run in Playnite. Every claim is read from code or docs; what would need a run to confirm is listed under [Not verified](#not-verified).

---

## TL;DR

1. **One mechanism, the same for a theme and an extension.** An add-on ships a `Localization/` folder at its root holding WPF `ResourceDictionary` XAML files of `sys:String` entries: `en_US.xaml` (required) plus `{code}.xaml` per language. Playnite merges `en_US.xaml`, then the current language's file on top, into the application-wide resources.
2. **English is loaded underneath.** A key missing from the chosen language, or present but empty, shows the add-on's `en_US.xaml` value. A key missing from both shows nothing in XAML and `<!KEY!>` from C#.
3. **A theme can ship its own strings and needs no extension to resolve them.** Playnite loads the theme's `Localization/` folder itself as it applies the theme.
4. **All strings share one global namespace.** A theme's XAML can use a key the extension ships (and the reverse) with `{DynamicResource KEY}`; if the extension is absent the text is simply empty. Keys must be unique across every installed add-on and Playnite itself.
5. **The file format is fixed by Playnite: XAML, not a flat key=value file.** Whatever a translator edits in the repo, what ships in each package must be `Localization/{code}.xaml`, unless the Bundle does its own loading.
6. **The language is read once, at start.** `IPlayniteSettingsAPI.Language` gives it to C# (`en_US` for English, else e.g. `cs_CZ`). Changing it is only possible in Desktop mode's settings and prompts for a restart; nothing reloads strings live.
7. **Playnite offers English plus 37 language codes** (list below). An add-on file is only ever loaded if its name equals the code Playnite is set to.

---

## 1. How an extension ships per-language files, and which get loaded

The loader is one static method, [`Localization.LoadAddonLocalization(addonDir)`](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Localization.cs#L192-L247):

- It looks for `<addon dir>/Localization/` (`PlaynitePaths.LocalizationsDirName = "Localization"`, [PlaynitePaths.cs#L30](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Settings/PlaynitePaths.cs#L30)). No folder: nothing happens.
- **`en_US.xaml` must exist.** Without it the method returns before loading anything, so a folder with only `de_DE.xaml` is ignored even when Playnite is set to German.
- It loads `en_US.xaml`, always.
- If the current language is neither `english` nor `en_US`, and `Localization/{CurrentLanguage}.xaml` exists, it loads that too, after English.
- No other files are read. There is no neutral-language fallback: `pt_BR` never falls back to `pt_PT`, and a file named `de.xaml` is never loaded.

Each file is parsed with `XamlReader.Load` ([Xaml.cs#L21-L27](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Common/Xaml.cs#L21-L27)) and must have a `ResourceDictionary` root. Then, per file:

- entries whose value is **not a string are removed**;
- entries whose value is an **empty string are removed** (so English shows through);
- the dictionary is appended to `Application.Current.Resources.MergedDictionaries`.

A file that fails to parse is logged and skipped; the other file still loads.

For a plugin this runs in `ExtensionFactory.LoadPlugins`, **before the plugin's assembly is loaded and its constructor runs** ([ExtensionFactory.cs#L420](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Plugins/ExtensionFactory.cs#L420)), so the strings are available from the constructor onwards. Script extensions get the same call ([#L365](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Plugins/ExtensionFactory.cs#L365)).

The docs describe the same contract and give the file shape ([extensions/localizations.md](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/extensions/localizations.md)):

```xml
<?xml version="1.0"?>
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:sys="clr-namespace:System;assembly=mscorlib">
    <sys:String x:Key="LOCTestPluginSomeString">Test string</sys:String>
    <sys:String x:Key="LOCTestPluginMultilineString" xml:space="preserve">Test string
on multiple lines</sys:String>
</ResourceDictionary>
```

Reading a string: `ResourceProvider.GetString("key")` from C#, `{DynamicResource key}` from XAML (same docs page).

**Consequences for the file format.** The format is XML: `&`, `<` and quotes in a value need XML escaping, a multi-line value needs `xml:space="preserve"`, and one malformed entry makes the whole file fail to parse (the language then silently shows English). It is one flat file per language and editable in GitHub's web editor, but it is not a plain `key = value` file.

## 2. Can a theme ship strings of its own, without the extension?

**Yes.** `ThemeManager.ApplyTheme` calls the same `LoadAddonLocalization(theme.DirectoryPath)` after it has merged the theme's XAML files ([Themes.cs#L210-L218](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Themes.cs#L210-L218)). Playnite does this itself, so the theme's strings resolve with no extension installed. The rules are identical to section 1: `Localization/en_US.xaml` required, `Localization/{code}.xaml` on top.

The docs page for themes says the same and tells themes to reference strings with `{DynamicResource stringkey}` ([themes/localizations.md](https://github.com/Playnite/Docs/blob/41d6529c7b96335211777031e5a7b36b86cd982a/docs/tutorials/themes/localizations.md); its heading and "root of your extension" wording are copied from the extension page, but the example keys are `LOCTestTheme...`).

Two details:

- If loading the theme's localisation *throws*, `ApplyTheme` returns an error and Playnite falls back to the default theme ([PlayniteApplication.cs#L348-L352](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/App/PlayniteApplication.cs#L348)). A malformed file does not throw in a normal run (the parse error is caught and logged inside the loader), so in practice a bad file costs that language, not the theme.
- This repo's theme build copies everything under `theme/XboxSeries.Dashboard/` into the `.pthm` (`XboxSeries.Dashboard.proj`, the `DashboardFile` glob), so a `Localization/` folder there would be packed with no build change. That file's own comment notes tests read every XAML file under the theme folder, so those tests would see the new files.

## 3. Can a theme's XAML use strings the extension ships?

**Yes, by key, with `DynamicResource`. There is no per-add-on scope.** Every add-on's dictionary is appended to the one application-level `MergedDictionaries` collection, the same place Playnite's own `LOC...` strings live ([FullscreenApp/App.xaml#L9](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite.FullscreenApp/App.xaml#L9) merges `LocSource.xaml`). Any XAML or code in the Playnite process can resolve any key, whoever shipped it. The docs warn about exactly this: "String keys are shared between all extensions so make sure you use unique keys", recommending `LOC<extension_name>KeyValue`.

Order at start-up, in Fullscreen mode:

1. `Localization.SetLanguage(AppSettings.Language)` ([PlayniteApplication.cs#L338](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/App/PlayniteApplication.cs#L338)).
2. `ThemeManager.ApplyTheme(...)`: the theme's XAML, then the theme's strings ([#L348](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/App/PlayniteApplication.cs#L348)).
3. `Extensions.LoadPlugins(...)`: each plugin's strings, then the plugin, and only after that `MainModel.OpenView()` ([FullscreenApplication.cs#L173-L189](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite.FullscreenApp/FullscreenApplication.cs#L173-L189)).

So the extension's strings are merged after the theme's files are parsed but before the main window exists. What follows from that:

- `{DynamicResource KEY}` in the theme resolves an extension-shipped key, because it is looked up when the element is realised and tracked afterwards.
- **If the extension is not installed, the key is in no dictionary and the text is blank.** No error, no English. A string the Dashboard must show without the extension has to be in the theme's own `Localization/`.
- `{StaticResource KEY}` is the wrong tool for strings: the theme's top-level dictionaries are parsed before either add-on's strings are merged.
- **The same key may be shipped by both packages.** WPF allows duplicate keys across merged dictionaries and the last-added dictionary wins ([ResourceDictionary.MergedDictionaries, Remarks](https://learn.microsoft.com/dotnet/api/system.windows.resourcedictionary.mergeddictionaries)). The extension's files are added after the theme's, so with both installed the extension's value is used; with only the theme, the theme's. Shipping one shared set of files in both packages is therefore legal.

## 4. A key missing from the chosen language

**English is loaded underneath, per add-on.**

- Both files are merged, English first, the chosen language second. WPF searches the last-added merged dictionary first and stops at the first hit, so the translation wins where it has the key and `en_US.xaml` answers where it does not ([Microsoft: merged dictionary behaviour](https://learn.microsoft.com/dotnet/desktop/wpf/systems/xaml-resources-merged-dictionaries#merged-dictionary-behavior)).
- A key present with an empty value is removed at load and falls back the same way ([Localization.cs#L202-L215](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Localization.cs#L202-L215)). Playnite does this for its own files because Crowdin exports untranslated strings as empty entries (comment at [#L164-L165](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Localization.cs#L164-L165)).
- A language with no file at all: only `en_US.xaml` is loaded, so everything is English.
- A key in neither file: `{DynamicResource}` renders nothing; `ResourceProvider.GetString` returns the literal `<!KEY!>` ([PlayniteSDK/ResourceProvider.cs#L59-L70](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/PlayniteSDK/ResourceProvider.cs#L59-L70)).
- An extra key in a translation that English lacks is loaded and usable; nothing validates one file against the other.

So a partial translation works with no code in the Bundle, provided the English file is named `en_US.xaml`.

## 5. Reading Playnite's current language from C#

`IPlayniteSettingsAPI.Language`, reached as `PlayniteApi.ApplicationSettings.Language`. It is in SDK 6.18.0 (`P:Playnite.SDK.IPlayniteSettingsAPI.Language` in the package's XML docs, with an empty summary). The implementation ([PlayniteSettingsAPI.cs#L65](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/API/PlayniteSettingsAPI.cs#L65)):

```csharp
public string Language => settings.Language == "english" ? "en_US" : settings.Language;
```

- The value is the language file's name: `en_US` for English (Playnite stores English internally as `english`, the setting's default, [PlayniteSettings.cs#L1142-L1157](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Settings/PlayniteSettings.cs#L1142-L1157)), otherwise a code such as `cs_CZ`, with an underscore.
- **It is not a .NET culture name, and Playnite does not set the thread culture.** Playnite builds a `CultureInfo` for the language (`code.Replace("_", "-")`) but uses it only to decide right-to-left; nothing assigns `CurrentCulture` or `CurrentUICulture`, and the one line that would set the WPF `Language` on windows is commented out ([WindowBase.cs#L223-L225](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Controls/WindowBase.cs#L223-L225)). Dates and numbers formatted by .NET therefore keep following Windows, whatever Playnite's language is.
- For most uses the Bundle does not need the code at all: `ResourceProvider.GetString(key)` (static, in the SDK) and `PlayniteApi.Resources.GetString(key)` already return the string in the current language.
- `Core` is Playnite-free, so it cannot call either; a string it needs has to be passed in or looked up through an adapter the extension project supplies.

## 6. The language codes Playnite offers

Playnite's language list is English plus every `*.xaml` in its install's `Localization` folder whose name matches `[a-zA-Z]+_[a-zA-Z]+`; the file name without extension is the language's id ([Localization.cs#L84-L142](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Localization.cs#L84-L142)). At `10.64` the folder ([source/Playnite/Localization](https://github.com/JosefNemec/Playnite/tree/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Localization)) holds 37 language files:

`ar_SA` `bg_BG` `ca_ES` `cs_CZ` `da_DK` `de_DE` `el_GR` `es_ES` `et_EE` `fa_IR` `fi_FI` `fr_FR` `gl_ES` `he_IL` `hr_HR` `hu_HU` `id_ID` `it_IT` `ja_JP` `ko_KR` `lt_LT` `mr_IN` `nl_NL` `no_NO` `pl_PL` `pt_BR` `pt_PT` `ro_RO` `ru_RU` `sk_SK` `sr_SP` `sv_SE` `tr_TR` `uk_UA` `vi_VN` `zh_CN` `zh_TW`

plus English, whose id is `english` internally and `en_US` to add-ons. Notes:

- The codes are Crowdin's `%locale_with_underscore%` ([crowdin.yml](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/crowdin.yml)), not always the code one would guess: Serbian is `sr_SP`, Norwegian `no_NO`. An add-on file must use Playnite's exact spelling or it is never loaded.
- All 37 are offered regardless of how complete Playnite's own translation is; the list just shows the percentage beside incomplete ones (`da_DK` is at 25%).
- Three are right-to-left: `ar_SA`, `fa_IR`, `he_IL`. For those Playnite sets `FlowDirection = RightToLeft` on every Playnite window, **mirroring the layout**, unless the theme's manifest sets `ForceLTRLayout: true` ([WindowBase.cs#L233-L237](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Controls/WindowBase.cs#L233-L237), [ThemeManifest.cs#L19](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Manifests/ThemeManifest.cs#L19), [Themes.cs#L220](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Themes.cs#L220)). This repo's `theme.yaml` does not set it today, so the Dashboard is already mirrored for those three languages. The map puts RTL mirroring out of scope; that flag is how to turn Playnite's mirroring off.
- **A language Playnite does not offer cannot be selected**, so an add-on file for it (a pseudo-language, or a real language Playnite lacks) is never loaded through the normal path. See [Not verified](#not-verified) for a possible way round.

## 7. Does Playnite restart on a language change?

**It asks to; it never switches live.**

- `PlayniteSettings.Language` carries `[RequiresRestart]` ([PlayniteSettings.cs#L1143](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Settings/PlayniteSettings.cs#L1143)). When Desktop mode's settings are saved with such a field changed, Playnite shows a Yes/No prompt and restarts on Yes ([SettingsViewModel.cs#L600-L614](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite.DesktopApp/ViewModels/SettingsViewModel.cs#L600-L614)). On No, Playnite keeps running in the old language until its next start.
- `Localization.SetLanguage` and `LoadAddonLocalization` are called only during start-up (the call sites in sections 1 to 3 are all of them). Nothing re-reads add-on strings while Playnite runs.
- **Fullscreen mode has no language setting.** Nothing under `Playnite.FullscreenApp` references the `Language` setting; the picker is in Desktop mode's settings only ([General.xaml#L39-L45](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite.DesktopApp/Controls/SettingsSections/General.xaml#L39-L45)). The Dashboard's own Settings cannot offer it through Playnite's settings bindings.
- On the very first run Playnite picks the language itself: the Windows UI culture, if Playnite has a file for it that is more than 75% translated ([PlayniteApplication.cs#L326-L336](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/App/PlayniteApplication.cs#L326-L336)).

This matches the map's settled point that the language is read as Playnite starts: reading it once is correct, not a simplification.

---

## What this fixes for the translation base

- **Shipped format:** `Localization/en_US.xaml` plus `Localization/{Playnite code}.xaml`, `ResourceDictionary` of `sys:String`, in each package that needs the strings. If the repo's source of truth is this XAML, no build step and no C# are needed, and fallback to English is Playnite's. If the source of truth is any other format, something has to generate these files at build time, or the Bundle has to load strings itself.
- **Lookup from the Dashboard:** `{DynamicResource KEY}`. Works without the extension only for keys in the theme's own `Localization/`.
- **Lookup from the extension:** `{DynamicResource KEY}` in XAML, `ResourceProvider.GetString(KEY)` in C#; neither is available to `Core`.
- **Keys:** global across Playnite and all add-ons, so they need a Bundle prefix. A key in neither file fails quietly (blank, or `<!KEY!>`), so only a test catches a typo.
- **One set for both packages:** allowed. The same files can ship in both; duplicates are legal and the extension's copy wins when both are installed.
- **Languages:** file names are limited to Playnite's 37 codes plus `en_US`.

## Not verified

- **Nothing was run.** All of the above is read from source and docs. The behaviours that rest on WPF rules rather than on Playnite code I could read line by line are: the extension's value winning over the theme's for a duplicated key, a `DynamicResource` in the theme picking up a key the extension merges later, and a blank (rather than an error) for a key nobody ships. They follow from Microsoft's documented lookup rules and the start-up order, but have not been seen on screen.
- **Selecting a language Playnite does not offer (the pseudo-language).** From the code, `SetLanguage` stores whatever string the `Language` setting holds as `CurrentLanguage`, even when Playnite has no file of that name (it then keeps its own strings in English), and the add-on loader looks for `{CurrentLanguage}.xaml`. So hand-editing `Language` in Playnite's `config.json` to, say, `qps_PLOC`, or dropping a file of that name into Playnite's install `Localization` folder, should make add-ons load `qps_PLOC.xaml`. Untested, and two risks are visible: `new CultureInfo("qps-PLOC")` is only reached when Playnite has the file and may throw for a name Windows does not know, and the settings combo box would show no selection. Reusing a real code Playnite offers avoids both.
- **Whether `IPlayniteSettingsAPI.Language` exists in SDK versions before 6.18.0.** Only 6.18.0 was checked, which is what the repo builds against.
- **Right-to-left on the extension's own windows.** Playnite sets `FlowDirection` in its `WindowBase` constructor. Whether the Guide, Toasts and other windows the extension creates are `WindowBase` instances (and so mirror) was not traced.
- **Fonts for non-Latin scripts** are outside this ticket and were not looked at.
- **The docs site itself** (api.playnite.link) was not fetched; the pages were read from the docs repo that generates it.
