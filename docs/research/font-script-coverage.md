# Which scripts the Bundle's fonts can draw

Research for [#166](https://github.com/snrgarb/xbox-series-theme/issues/166) (part of the map [#164](https://github.com/snrgarb/xbox-series-theme/issues/164)). Researched 2026-11-11.

**Baselines.** This repo at `7e05668`. Playnite `master` @ [`e2e6efc`](https://github.com/JosefNemec/Playnite/tree/e2e6efcda311fea38fcd92f61c8ca90811d97f28) (10.64 is the latest release, 2026-11-09). WPF source: `dotnet/wpf` `main` @ [`1fdaaaa`](https://github.com/dotnet/wpf/tree/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a); `WPF` below = `https://github.com/dotnet/wpf/blob/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a/src/Microsoft.DotNet.Wpf/src`.

**How far this was checked.** Everything here was read, nothing was rendered. The repo holds no font files, this was researched on Linux with no Windows fonts to hand, and no Windows VM was started. Font coverage comes from Microsoft's typography pages, not from the fonts' `cmap` tables. The fallback table was read from the open-source WPF, which is not the .NET Framework WPF Playnite runs on. See [What stayed unverified](#what-stayed-unverified).

## TL;DR

- The Bundle names **five font resources, once**, in `theme/XboxSeries.Dashboard/Constants.xaml`. Two text families, **Segoe UI** (body) and **Bahnschrift** (headers), and one icon font pair. All are Windows system fonts; **no font file ships with the Bundle**. The extension's windows have no fonts of their own: they read the Dashboard's resources.
- **Segoe UI** draws Latin (extended, Vietnamese included), Greek and Cyrillic, plus Armenian, Georgian, Arabic and Hebrew. **Bahnschrift** draws Latin (extended, Vietnamese), Greek and Cyrillic only. **Neither has a single Chinese, Japanese, Korean or Thai glyph.**
- WPF fills the gap by itself, per character, from its built-in composite font **Global User Interface**. With nothing done, Japanese lands on **Yu Gothic UI**, Korean on **Malgun Gothic**, Thai on **Leelawadee UI**; all three are in every Windows 10 and 11 install. So Cyrillic, Greek, Japanese, Korean and Thai should draw on arrival, with no tofu.
- **Chinese is the one that goes wrong.** Which font draws a Han character depends on the element's `Language` (`xml:lang`), and that is `en-US` everywhere today: Playnite does not set it (it did for two releases and took it back out, see below), and neither does the Bundle. Under `en-US` a Han character goes to Yu Gothic UI first, so Chinese text is drawn in Japanese glyph shapes, with the characters Yu Gothic UI lacks filled from a Chinese font in the same line. This is Playnite's open issue [#3710](https://github.com/JosefNemec/Playnite/issues/3710).
- Two levers fix it. **Set `Language`** on the element tree and the built-in fallback picks Microsoft YaHei UI (`zh-Hans`), Microsoft JhengHei UI (`zh-Hant`), Yu Gothic UI (`ja`) or Malgun Gothic (`ko`). Or **name the fallback in the font resource itself** (`Segoe UI, Microsoft YaHei UI`), which works whatever `Language` is. Both are sketched in [Setting the fallback per language](#setting-the-fallback-per-language); neither was tried.

## 1. The fonts the Bundle uses

### One place names them

[`theme/XboxSeries.Dashboard/Constants.xaml` L198-L204](../../theme/XboxSeries.Dashboard/Constants.xaml):

| Resource key | Value | Kind |
| --- | --- | --- |
| `XboxFont` | `Segoe UI` | text, body |
| `XboxFontSemibold` | `Segoe UI Semibold` | text, body |
| `XboxHeaderFont` | `Bahnschrift` | text, headers |
| `XboxHeaderFontSemibold` | `Bahnschrift SemiBold` | text, headers |
| `XboxIconFont` | `Segoe Fluent Icons, Segoe MDL2 Assets` | icons |

- No `.ttf`, `.otf`, `.ttc` or `.CompositeFont` file is tracked in the repo (`git ls-files`). Every font is expected from Windows.
- No other XAML names a font. `FontsXamlTests.No_XAML_names_a_font_other_than_through_the_shared_resources` (`extension/XboxSeries.Core.Tests/FontsXamlTests.cs`) fails the build if a Dashboard or extension XAML file sets a `FontFamily` any other way, and `Each_shared_font_is_named_once_in_Constants` pins the four text values above. A change to those values has to change that test.
- The one font set in C#, `ButtonGlyphs.cs` L122, also goes through the `XboxFont` key.
- Nothing in the theme or extension sets `xml:lang`, `Language` or `FlowDirection` (grep over both trees).

### The extension's windows

The extension defines no font. Each window reaches the Dashboard's keys with `{DynamicResource ...}`, found in Playnite's application resources where Playnite merges the theme:

| Surface | File | Keys used |
| --- | --- | --- |
| Guide | `OverlayWindow.xaml` (`GuideText`, `GuideIcon` styles, L30-L45) | `XboxFont`, `XboxHeaderFont`, `XboxIconFont` |
| Toasts | `ToastWindow.xaml`, `AchievementToast.xaml`, `ToastImages.xaml` | `XboxFont`, `XboxFontSemibold`, `XboxIconFont` |
| Start-up | `StartupWindow.xaml` | `XboxFont`, `XboxHeaderFont`, `XboxIconFont` |
| Idle screen | `DimWindow.xaml`, `IdleCardControl.xaml` | `XboxFont` |
| Console keyboard | `ConsoleKeyboardControl.xaml`, `TextEntryControl.xaml` | `XboxFont`, `XboxHeaderFont`, `XboxIconFont` |
| Home, Search, Settings, menus | `PromoRow.xaml`, `SearchControl.xaml`, `SettingsControl.xaml`, `MenuPanel.xaml`, `HintBarControl.xaml`, `StatusArea.xaml` | all five |

Two details matter to anyone changing the fonts:

- **Start-up runs on its own thread** and cannot read application resources, so `StartupResources.cs` copies `XboxFont`, `XboxHeaderFont` and `XboxIconFont` for it on the UI thread. A font swapped after that copy never reaches the Start-up window.
- **`XboxFont` doubles as the "is the Dashboard loaded" signal.** `DashboardPresence.cs` treats finding the `XboxFont` key in application resources as proof the Dashboard theme is active. If the extension ever puts its own `XboxFont` into application resources, that test turns true on every theme.

### Icon fonts versus text fonts

`XboxIconFont` only ever draws Private Use Area code points (`&#xE700;`, `&#xE77B;`...). They mean nothing in any language and no translation touches them. Segoe Fluent Icons ships with Windows 11 only, Segoe MDL2 Assets with 10 and 11 ([Windows 11 font list](https://learn.microsoft.com/en-us/typography/fonts/windows_11_font_list), [Windows 10 font list](https://learn.microsoft.com/en-us/typography/fonts/windows_10_font_list)), which is why the resource lists both.

The Bundle already leans on font fallback once: the trophy `&#x1F3C6;` is drawn with `XboxFont` (`AchievementToast.xaml` L347, `PromoRow.xaml` L132, `OverlayWindow.xaml` L156). Segoe UI has no emoji, so that glyph arrives through the fallback map `1F000-1F6FF` → `Segoe UI Emoji, Segoe UI Symbol`.

## 2. Which scripts each text font covers

From Microsoft's typography pages. "Script tags" are the `dlng`/`slng` values of the font's `meta` table as Microsoft publishes them; "code pages" are its OS/2 code page bits.

| | Segoe UI ([page](https://learn.microsoft.com/en-us/typography/font-list/segoe-ui)) | Bahnschrift ([page](https://learn.microsoft.com/en-us/typography/font-list/bahnschrift)) |
| --- | --- | --- |
| Script tags | `Latn`, `Grek`, `Cyrl`, `Armn`, `Geor`, `Geok`, `Arab`, `Hebr`, `Lisu` | `Latn`, `Cyrl`, `Grek`, `Vi-Latn`, `mk-Cyrl`, `bg-Cyrl`, `sr-Cyrl` |
| Code pages | 1252 Latin 1, 1250 Eastern Europe, 1251 Cyrillic, 1253 Greek, 1254 Turkish, 1255 Hebrew, 1256 Arabic, 1257 Baltic, 1258 Vietnamese | 1252, 1250, 1251, 1253, 1254, 1257, 1258 |
| Latin extended | yes | yes |
| Cyrillic | yes | yes |
| Greek | yes | yes |
| Chinese | **no** | **no** |
| Japanese | **no** | **no** |
| Korean | **no** | **no** |
| Thai | **no** | **no** |
| Arabic, Hebrew | yes | **no** |

- Segoe UI: "supports a very wide range of languages and scripts, including the Latin, Greek, and Cyrillic alphabets, Arabic, Hebrew, Armenian, Georgian, Vietnamese, Lisu".
- Bahnschrift: "support for over 200 Latin languages (including Vietnamese!), Greek and Cyrillic (with localized forms for Macedonian, Serbian, and Bulgarian)". Its extended Latin, Greek and Cyrillic arrived with **Windows 10 version 1809** (changelist on the same page); on an older Windows 10 a Cyrillic or Greek header falls back like any other missing glyph.
- "Segoe UI Semibold" and "Bahnschrift SemiBold" are weights of the same two families, so the coverage is the same. That the semibold faces match glyph for glyph was not checked.

So for the scripts the map asks about: **Latin extended, Cyrillic and Greek are drawn by the Bundle's own fonts; Chinese, Japanese, Korean and Thai are drawn entirely by fallback**, in body text and headers alike.

## 3. What WPF does for a glyph the font lacks

### The chain

From [`FontFamily`, "Font Fallback"](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.fontfamily#font-fallback):

1. The fonts in the `FontFamily` string are tried in order. `"Comic Sans MS, Verdana"`: "if the glyphs from the "Comic Sans MS" typeface are not available, glyphs from the "Verdana" typeface are used."
2. "If neither ... have the required glyphs, the fallback font family of the typeface is used, which is "Global User Interface" by default."
3. Global User Interface is a **composite font** (`GlobalUserInterface.CompositeFont`): a list of `FontFamilyMap` entries, each "defines which `FontFamily` to use for a specified set of Unicode code points and a culture-specific language" ([`FontFamilyMap`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.fontfamilymap)). Each entry's `Target` is itself an ordered list of fonts.

The substitution is per character, so the Latin in a mixed string stays in Segoe UI or Bahnschrift. The same mechanism covers a font that is missing altogether. "The WPF font fallback mechanism replaces previous Win32 font substitution technologies": the registry's `FontLink\SystemLink` plays no part.

### Which Windows fonts it lands on

Read from the composite font in the WPF source, [`WPF/PresentationCore/Fonts/GlobalUserInterface.CompositeFont`](https://github.com/dotnet/wpf/blob/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a/src/Microsoft.DotNet.Wpf/src/PresentationCore/Fonts/GlobalUserInterface.CompositeFont), section `OS="Windows10RS5"` (the file has one section per Windows generation, and the parser takes the first one the running OS satisfies: [`CompositeFontParser.cs`](https://github.com/dotnet/wpf/blob/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a/src/Microsoft.DotNet.Wpf/src/PresentationCore/MS/internal/FontFace/CompositeFontParser.cs)). The first font in each list that has the glyph wins.

| Script (range) | Element language | Fonts tried, in order |
| --- | --- | --- |
| Latin, Greek, Cyrillic, Hebrew (`0000-052F`, `1D00-1FFF`...) | any | Segoe UI, Segoe UI Symbol, Ebrima |
| Arabic (`0600-06FF`...) | any | Segoe UI, Simplified Arabic |
| Thai, Lao (`0E00-0EFF`) | any | **Leelawadee UI** |
| Hangul (`1100-11FF`, `3130-318F`, `AC00-D7FF`...) | any | **Malgun Gothic**, Gulim |
| Kana, CJK punctuation (`3000-30FF`, `31F0-31FF`) | `zh-Hans` | Microsoft YaHei UI, Microsoft YaHei, SimSun, MS Gothic |
| | `zh-Hant` | Microsoft JhengHei UI, Microsoft JhengHei, MingLiU, MS Gothic |
| | `ja` | Yu Gothic UI, Meiryo UI, Meiryo, Microsoft YaHei UI, Microsoft YaHei, MS Gothic, MingLiU |
| | `ko` | Malgun Gothic, Yu Gothic UI, Meiryo UI, Meiryo, Microsoft YaHei UI, ... |
| | none of these | **Yu Gothic UI**, Meiryo UI, Meiryo, Microsoft YaHei UI, Microsoft YaHei, MS Gothic, MingLiU |
| Han ideographs (`3400-4DBF`, `4E00-9FFF`) | `zh-Hans` | **Microsoft YaHei UI**, Microsoft YaHei, SimSun, SimSun-18030, SimSun-ExtB, MingLiU |
| | `zh-HK` | Microsoft JhengHei UI, Microsoft JhengHei, Microsoft YaHei UI, Microsoft YaHei, MingLiU_HKSCS, ... |
| | `zh-Hant` | **Microsoft JhengHei UI**, Microsoft JhengHei, Microsoft YaHei UI, Microsoft YaHei, MingLiU, ... |
| | `ja` | **Yu Gothic UI**, Meiryo UI, Meiryo, MS Gothic, Microsoft JhengHei UI, ... |
| | `ko` | **Malgun Gothic**, Gulim, Microsoft JhengHei UI, Microsoft JhengHei, Microsoft YaHei UI, ... |
| | none of these | **Yu Gothic UI**, Meiryo UI, Meiryo, Microsoft JhengHei UI, Microsoft JhengHei, Microsoft YaHei UI, Microsoft YaHei, MS Gothic, MingLiU, SimSun |
| Fullwidth forms (`FF00-FF60`) | `zh-Hans` / `zh-Hant` / `ko` / other | YaHei UI / JhengHei UI / Malgun Gothic / Yu Gothic UI first |
| Emoji (`1F000-1F6FF`, `1F900-1F9FF`) | any | Segoe UI Emoji, Segoe UI Symbol |

Every `Scale` in these maps is `1.0`: the fallback font is drawn at the same em size, with its own metrics.

Are those fonts on the PC? Per Microsoft's font lists for [Windows 11](https://learn.microsoft.com/en-us/typography/fonts/windows_11_font_list) and [Windows 10](https://learn.microsoft.com/en-us/typography/fonts/windows_10_font_list):

- **In every install** (the base list): Segoe UI, Bahnschrift, Segoe UI Symbol, Segoe UI Emoji, **Yu Gothic UI**, **Microsoft YaHei UI**, **Microsoft JhengHei UI**, **Malgun Gothic**, **Leelawadee UI**, Nirmala UI, MS Gothic, SimSun. Each of the five script fonts has a bold face in the base list too (Yu Gothic UI Semibold and Bold, YaHei UI Bold, JhengHei UI Bold, Malgun Gothic Bold, Leelawadee UI Bold).
- **Only with an optional "Supplemental Fonts" feature**: Meiryo and Meiryo UI (Japanese), Gulim (Korean), MingLiU (Traditional Chinese), Leelawadee (Thai), Simplified Arabic (Arabic). The fallback lists name them but never need them first.

So on a stock English Windows 10 or 11 the first font in each bold row above is present, and none of the five scripts depends on a language pack.

### How the language decides the Han glyph

Chinese, Japanese and Korean share Han code points whose correct shapes differ by language; Microsoft's own wording: "Chinese, Japanese, and Korean share code points in Unicode, but the ideographic shapes are different and they use totally different fonts" ([Globalization for WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/globalization-for-wpf#language-attribute)). WPF chooses by the element's language:

- `xml:lang` "changes the behavior of hyphenation, spell checking, number substitution, complex script shaping, and font fallback" ([WPF globalization overview, best practices](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview#best-practices-for-globalization-and-localization-in-wpf)).
- `xml:lang` and [`FrameworkElement.Language`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.frameworkelement.language) are the same thing. The property **inherits down the element tree**, and its **default is `en-US`**. It is not taken from the Windows display language, the thread's culture or Playnite's setting.
- A `FontFamilyMap`'s [`Language`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.fontfamilymap.language) is "a specific language", "a neutral language if it applies to a group of related languages", or empty "if it applies to any language".
- Matching (read from source, not run): a map with no language matches everything; otherwise the map's language must include the element's, by tag prefix or by walking `CultureInfo.Parent`, so `zh-Hans` covers `zh-CN` and "`zh-hant` includes `zh-hk`" ([`FamilyMap.cs` `MatchLanguage`](https://github.com/dotnet/wpf/blob/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/FamilyMap.cs#L182-L196), [`XmlLanguage.cs` `RangeIncludes`](https://github.com/dotnet/wpf/blob/1fdaaaab9b20a0c00c3a53fa1a61d047fac96d2a/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Markup/XmlLanguage.cs#L314-L324)). Maps are searched in file order, and the language-specific ones sit before the any-language one for the same range.

What that means with `Language` left at `en-US`, which is today's state (inferred from the table, not rendered):

| Text | Drawn with | Verdict |
| --- | --- | --- |
| Japanese | Yu Gothic UI for kana and kanji | right |
| Korean | Malgun Gothic for Hangul; a rare hanja would come from Yu Gothic UI | right in practice |
| Thai | Leelawadee UI | right |
| Russian, Greek, Polish, Vietnamese... | Segoe UI and Bahnschrift themselves | right |
| Simplified Chinese | Yu Gothic UI for every character it has, then Microsoft JhengHei UI, then Microsoft YaHei UI for the rest | **wrong**: Japanese shapes, and up to three fonts mixed in one line |
| Traditional Chinese | Yu Gothic UI, then Microsoft JhengHei UI | **wrong**: Japanese shapes, two fonts mixed |

### Playnite does not set the language

- For a short while Playnite set `Language` on every window from the app's language, to fix exactly this: PR [#4359](https://github.com/JosefNemec/Playnite/pull/4359), "Fix locale-aware CJK font fallback in Fullscreen mode", merged 2026-08-13. By the dates it shipped in 10.57 and 10.58 (both 2026-09-10); the bug below was reported on 10.58.
- 10.59 (2026-09-11) took it back out, because it broke the saving of the Language and Theme combo boxes ([#4385](https://github.com/JosefNemec/Playnite/issues/4385)). The line is commented out on `master` today: [`WindowBase.cs` L223-L225](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Controls/WindowBase.cs#L223-L225), "Disabled as this breaks combobox binding on multiple places".
- The original bug, [#3710](https://github.com/JosefNemec/Playnite/issues/3710) "Full screen mode menus display simplified chinese text incorrectly", is open.

So from 10.59 on, every Playnite window, and so the whole Dashboard, runs at `en-US`. The extension's own windows (Guide, Toasts, Start-up, Idle screen, Launch screen) are plain WPF windows the extension creates, and they are at `en-US` too.

## Setting the fallback per language

None of these was tried. All three are WPF features Microsoft documents; what is uncertain is how each behaves inside Playnite.

### A. Set `Language` and let the built-in fallback choose

`Language` inherits, so one assignment at a root covers everything under it. `XmlLanguage.GetLanguage("zh-CN")` in code, or `xml:lang="zh-CN"` in XAML.

- **Extension windows**: the extension constructs them, so it can set `Language` on each window as it builds it. The Start-up window has its own thread and its own tree and needs the same line.
- **Dashboard**: a theme is XAML only and cannot compute anything, and a literal `xml:lang` in a theme file can't follow Playnite's setting. Two routes, both untested: the extension sets `Language` on Playnite's windows from code (this is what Playnite did in 10.57 and backed out, so it risks bringing #4385 back), or the theme's templates set `Language="{DynamicResource ...}"` on their own root panels, below Playnite's windows and controls, with the extension supplying the `XmlLanguage` resource.
- **Side effect to weigh**: `Language` is also the culture of every binding. "If you do not set [ConverterCulture], the binding engine uses the `Language` property of the binding target object. In XAML this defaults to "en-US"" ([`Binding.ConverterCulture`](https://learn.microsoft.com/en-us/dotnet/api/system.windows.data.binding.converterculture)). Setting `Language` changes how `StringFormat` bindings and value converters format numbers and dates under that element. That may be what broke Playnite's combo boxes (the cause was not traced), and it bears on the map's open question about dates and numbers.

### B. Name the fallback in the font resource

A `FontFamily` string is itself a fallback list, tried before Global User Interface. Because it comes first, it works at `en-US`:

```xml
<FontFamily x:Key="XboxFont">Segoe UI, Microsoft YaHei UI</FontFamily>
<FontFamily x:Key="XboxHeaderFont">Bahnschrift, Microsoft YaHei UI</FontFamily>
```

- Latin, Cyrillic and Greek still come from the first font; only what it lacks reaches the second.
- The list has to differ per language (YaHei UI for Simplified Chinese, JhengHei UI for Traditional, Yu Gothic UI for Japanese, Malgun Gothic for Korean), so something has to choose it at start: the theme's XAML can't. Every use is a `DynamicResource`, so a value the extension places in application resources would be picked up, but mind the two traps in section 1: the Start-up copy is taken once, and `XboxFont`'s presence is the Dashboard-loaded signal.
- Only Chinese needs this. Japanese, Korean and Thai already land on the right font.

### C. Ship a composite font

"You can specify your own font fallback preference by creating your own composite font and specifying what font to use for specific codepoint ranges and languages" ([WPF globalization overview](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview#examples-of-wpf-localization)). A `.CompositeFont` file is XML (`FontFamily` > `FontFamily.FamilyMaps` > `FontFamilyMap Unicode=... Language=... Target=... Scale=...`), and "can be referenced in any location by including its URI when referencing the family by name" ([`FontFamily`, "Composite Fonts"](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.fontfamily#composite-fonts)), in the form `folder-or-uri#Family Name`.

- It gives control B lacks: a `Scale` per range, and one resource value for all languages.
- But its per-language maps are chosen by the same element `Language` as the built-in ones. At `en-US` only its any-language maps apply, so **C does not remove the need for A**; without A it can only say "Han goes to font X for everyone".
- Whether Playnite's theme loading resolves a font URI relative to a theme file was not tested.

### In short

| Lever | Fixes Chinese at `en-US`? | Who can do it | Main risk |
| --- | --- | --- | --- |
| A. set `Language` | yes, and picks the right font for all four CJK variants | extension (its windows: easy; Playnite's: uncertain) | binding cultures change with it; #4385 |
| B. font list per language | yes | extension, at start | Start-up copy, Dashboard-loaded signal, `FontsXamlTests` |
| C. composite font | no, not alone | theme or extension | still needs A; URI resolution untested |

## Other things a translation will meet

- **Headers lose their look.** Bahnschrift has no CJK or Thai, so a Chinese, Japanese, Korean or Thai header is drawn wholly in the fallback UI font, the same one as body text.
- **Weights.** Text set with `FontWeight="Bold"` should find the fallback's bold face (all five script fonts have one). Text set by family name (`Segoe UI Semibold`, `Bahnschrift SemiBold`) asks for no weight, so its fallback glyphs may come out regular. Not verified.
- **Metrics.** Fallback fonts are drawn at scale 1.0 with their own ascent and line height. Layouts tuned to Segoe UI (fixed `LineHeight="45"` in the Toasts, negative margins in `IdleCardControl.xaml`) may clip or shift. This belongs with the map's "layouts that break on longer text".
- **The Toast's page measure uses a different culture from the text it measures.** `AchievementToast.xaml.cs` L86-L89 lays text out with `FormattedText(..., CultureInfo.CurrentUICulture, ...)`, while the `TextBlock` it predicts draws with its `Language` (`en-US`). When the two differ, the measure and the drawing can pick different fallback fonts for Han text and disagree on where a line wraps.
- **Thai.** WPF draws Thai, with a caveat from Microsoft: "the display and editing of Thai text is supported; word breaking is not" ([Globalization for WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/globalization-for-wpf#international-text)). Thai has no spaces between words, so wrapped Thai text may break mid-word or not wrap where expected. That page is old and was not checked against current behaviour.
- **Right-to-left.** Segoe UI draws Arabic and Hebrew; Bahnschrift does not, so RTL headers fall back to Segoe UI. Playnite added a `ForceLTRLayout: true` theme-manifest switch in September 2026 ([`WindowBase.cs` L233](https://github.com/JosefNemec/Playnite/blob/e2e6efcda311fea38fcd92f61c8ca90811d97f28/source/Playnite/Controls/WindowBase.cs#L233), commit `63e61d1`), which is relevant to the map's out-of-scope note on mirroring.

## What stayed unverified

1. **No font file was opened.** Coverage is Microsoft's published script tags and code pages, not a `cmap` dump. To check: on Windows, `fc-query` or fontTools against `C:\Windows\Fonts\segoeui.ttf`, `seguisb.ttf` and `bahnschrift.ttf`.
2. **The fallback table is the open-source WPF's, not .NET Framework's.** Playnite and this extension target .NET Framework 4.6.2 (`Directory.Build.props`), whose WPF source is not published. The mechanism is documented for .NET Framework by the same Learn pages, but the exact font lists per range, and that .NET Framework on Windows 10/11 uses the same `Windows10RS5` lists, are assumed. The file to read on a Windows PC is `GlobalUserInterface.CompositeFont`, in `C:\Windows\Fonts` or under the framework's `WPF\Fonts` folder (location from memory, not confirmed).
3. **Nothing was rendered.** No pseudo-language or real CJK/Thai string was drawn in the Dashboard or any extension window. The "right"/"wrong" verdicts in section 3 are read off the table.
4. **How much of Simplified Chinese Yu Gothic UI covers**, and so how mixed a line looks at `en-US`, was not measured.
5. **Lever A on the Dashboard**: whether `Language` set by `DynamicResource` inside a theme template works, and whether setting it on Playnite's windows from the extension reproduces #4385.
6. **Lever B**: that an application-level resource set by the extension overrides the theme's merged `XboxFont` for every `DynamicResource` use.
7. **Lever C**: font URI resolution from a Playnite theme file.
8. **Weight carried onto fallback fonts** for the two `... Semibold` family names; and how WPF on .NET Framework resolves `Bahnschrift SemiBold`, a named instance of a variable font, at all (it is in use today, so it evidently resolves; how was not looked at).
9. **Game descriptions** (`HtmlTextView` with `HtmlFontFamily`, `GameDetails.xaml` L886 and L932) go through Playnite's HTML renderer, whose fallback was not examined. They are not Bundle strings.
10. **Windows editions other than stock desktop Windows 10/11** (N, LTSC, trimmed images) were not checked for the five script fonts.
