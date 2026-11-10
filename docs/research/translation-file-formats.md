# String file formats a translator can edit in GitHub's web editor

Research for [#167](https://github.com/snrgarb/xbox-series-theme/issues/167), part of the map [#164](https://github.com/snrgarb/xbox-series-theme/issues/164). It compares the candidate formats and lays out the trade-offs. **It does not pick one**: that belongs to a later ticket.

Sources were read on 10 and 11 November 2026. Each claim names its source; **[inference]** marks reasoning from those sources that nobody has run. Nothing here was executed: this machine has no .NET SDK, so no parser was tried against a sample file. [What could not be verified](#what-could-not-be-verified) lists the gaps.

## What the map already fixes

From #164's notes: one flat file per language in the repo; the translator copies the English file, edits values in GitHub's web editor and opens a PR; no build, tools or C# on their side; a missing string falls back to English; the language follows Playnite's own setting; scripts beyond Latin must work.

## What this repo constrains

- **Target framework: .NET Framework 4.6.2** for every project (`Directory.Build.props`: `<TargetFramework>net462</TargetFramework>`). A library must ship a `net4x` build, or a `netstandard2.0` build at most. `netstandard2.1` and `net6.0+` builds cannot load.
- **`netstandard2.0` on 4.6.2 is the weak case.** Microsoft lists 4.6.2 as supporting .NET Standard 2.0 but notes "several issues with consuming .NET Standard libraries" on 4.6.1 and recommends 4.7.2 or higher. [MS: .NET Standard overview](https://learn.microsoft.com/en-us/dotnet/standard/net-standard?tabs=net-standard-2-0) The extension's csproj already strips the .NET Standard facades from the package because "4.7.2 and later, which Playnite runs on, have them built in" (`extension/XboxSeries.Extension/XboxSeries.Extension.csproj`).
- **Newtonsoft.Json 10.0.3 is already there for free.** Core references it and Playnite loads it itself, so the extension never ships it (`extension/XboxSeries.Core/XboxSeries.Core.csproj`). Any other library is a new DLL in the `.pext`, loaded into Playnite's process next to every other add-on's DLLs. **[inference]** A dependency such as `System.Memory` can then clash with another add-on's copy.
- **Core is Playnite-free and WPF-free** (its csproj says never to add a Playnite reference). Whatever the format, Core's C# strings need their own lookup seam.
- **A theme runs no code.** The Dashboard's XAML can only take a string from a WPF resource (`{DynamicResource Key}`) or from a binding to something the extension exposes. And the theme "must work, degraded, without the extension" (`docs/research/playnite-bundle-model.md`).
- **CI runs on `ubuntu-latest` and `windows-latest`** (`.github/workflows/ci.yml`). The Core tests run on Windows.

## What GitHub's web editor does and doesn't do

Clicking the pencil on a file in someone else's repository forks it and opens a pull request for the edit; the editor is CodeMirror. [GitHub Docs: Editing files](https://docs.github.com/en/repositories/working-with-files/managing-files/editing-files) That page says nothing of validation, encoding or line endings. **[inference]** So the editor highlights syntax but checks nothing: the first thing to tell a translator their file is broken is CI on the PR, or nothing at all.

So two properties matter more than they would with a desktop tool:

1. **How little syntax surrounds the text**, since nothing stops a stray character.
2. **What one mistake costs**: one string, or the whole language.

## Comparison

"Who's" is the map's test sentence, `Who's playing today?`. "Reader" is what would read the file on .NET Framework 4.6.2.

| Format | "Who's playing today?" as written | Characters that bite a hand editor | One mistake costs | Comments | Placeholders | Plurals | Reader on net462 | Dashboard XAML can use it | CI check |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| **Playnite XAML** (`Localization/xx_XX.xaml`) | `<sys:String x:Key="K">Who's playing today?</sys:String>` | `&` and `<` must be `&amp;` `&lt;`; tags and `x:Key="…"` must stay intact; leading and trailing spaces need `xml:space="preserve"` | The whole file: Playnite logs it and skips the language | `<!-- -->` | `{0}` by convention (`string.Format`) | None | **Playnite itself**: nothing to write or ship | **Natively**, even with no extension | XML well-formedness; the repo's own test |
| **JSON, flat** | `"K": "Who's playing today?"` | `"` and `\` must be escaped; a missing or trailing comma; no comments allowed | The whole file | None in the standard | `{0}` or `{name}` by convention | None; by key convention | **Newtonsoft.Json 10.0.3**, already loaded | Through a resource dictionary the extension fills | `jq` (preinstalled), JSON Schema |
| **JSON with ICU MessageFormat values** | `"K": "Who's playing today?"` (see the apostrophe caveat) | JSON's, plus `{` `}` and `'` before a brace; nested braces in plurals | The whole file for JSON syntax; one message for ICU syntax | None in the standard | `{name}`, typed (`{n, number}`) | **Yes**, CLDR categories | Newtonsoft + **MessageFormat** (`netstandard2.0` only, 76 KB + `System.Memory`) | Same as JSON | `jq`; the repo's own test to parse each message |
| **.resx** | `<data name="K" xml:space="preserve"><value>Who's playing today?</value></data>` | As XAML, plus a schema and header block that must survive | The whole file | `<comment>` per string | `{0}` by convention | None | **Built in**: `ResXResourceReader` (System.Windows.Forms.dll), or ~15 lines of `XDocument` | Through a filled dictionary; `x:Static` only with a build | XML well-formedness; the repo's own test |
| **gettext PO** | `msgid "Who's playing today?"` / `msgstr "…"` | `"` and `\` must be escaped; the header block; `msgid` lines must not change | Depends on the reader (Karambolo.PO returns diagnostics) | **Richest**: `# `, `#.`, `#:`, `#,` | printf-style or `{0}`, with format flags | **Yes**, a formula in the file's header | **Karambolo.PO** (`net40`/`net45` builds, 59 KB, MIT) | Through a filled dictionary | `msgfmt -c` (needs `apt install gettext`) |
| **Fluent** (`.ftl`) | `K = Who's playing today?` | `{` and `}`; a continuation line starting with `[`, `*` or `.`; multi-line indentation | One message (the rest of the file still loads) | `#`, `##`, `###` | `{ $name }` | **Yes**, CLDR categories | **Fluent.Net** (`netstandard2.0`, 103 KB, Apache-2.0, last pushed November 2024) | Through a filled dictionary | `moz-fluent-linter`; the repo's own test |
| **TOML** | `K = "Who's playing today?"` | `"` and `\` must be escaped; **single-quoted strings cannot hold an apostrophe** | The whole file | `#` | `{0}` by convention | None; sub-tables by convention | **Tommy** (`net35` build, 41 KB, MIT, last release January 2022); Tomlyn is 330 KB plus dependencies | Through a filled dictionary | `taplo` |
| **YAML** | `K: Who's playing today?` | `: ` and ` #` inside a value; a value starting with `{`, `[`, `'`, `"`, `*`, `&`, `!`, `%`, `@`; indentation; tabs | The whole file | `#` | `{0}` by convention, **but not at the start of a bare value** | None; nested keys by convention | **YamlDotNet** (`net47`/`netstandard2.0`, 298 KB, MIT) | Through a filled dictionary | `yamllint` (preinstalled) |
| **.properties** | `K = Who's playing today?` | `\` (escape and line continuation); leading spaces are dropped | One line | `#` or `!` | `{0}` by convention | None; key suffix by convention | **Hand-written**, some 30 to 50 lines; or Kajabity.Tools.Java (13 KB) | Through a filled dictionary | The repo's own test only |

## Format by format

### Playnite's XAML resource dictionaries

**The format.** A `ResourceDictionary` of `<sys:String x:Key="…">` elements. Playnite's own strings are 1,134 such entries in [`LocSource.xaml`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Localization/LocSource.xaml), with 38 language files beside it named `cs_CZ.xaml`, `ja_JP.xaml` and so on. [Playnite `source/Playnite/Localization`](https://github.com/JosefNemec/Playnite/tree/master/source/Playnite/Localization)

**Playnite loads it for add-ons, themes included.** An add-on puts `Localization/en_US.xaml` in its root, with other languages as `{locale}.xaml`; strings are read with `ResourceProvider.GetString("key")` in C# and `{DynamicResource key}` in XAML. [Playnite docs: extension localizations](https://github.com/JosefNemec/PlayniteDocs/blob/master/docs/tutorials/extensions/localizations.md), [theme localizations](https://github.com/JosefNemec/PlayniteDocs/blob/master/docs/tutorials/themes/localizations.md) The code behind it, `Localization.LoadAddonLocalization`:

- loads `en_US.xaml` first, then the current language's file on top, so **a missing key falls back to English for free**;
- removes entries that are empty strings, so an untranslated-but-present key falls back too;
- adds both to `Application.Current.Resources.MergedDictionaries`;
- is called for each extension (`ExtensionFactory.cs`) and for the theme (`Themes.cs`, added April 2022 as "SDK: Support for themes to use localized resources like extensions").

[Playnite `Localization.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Localization.cs), [`Themes.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Themes.cs), [`ExtensionFactory.cs`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Plugins/ExtensionFactory.cs)

**Keys are global.** "String keys are shared between all extensions", so the docs recommend a `LOC<extension_name>` prefix. [Playnite docs, as above] **[inference]** The same fact means one copy of the files could serve both halves of the Bundle: strings the extension's folder loads are visible to the Dashboard's `{DynamicResource}`. That is untested, and a theme installed alone would then have no strings.

**Hand edits.** Inside an element, apostrophes and straight quotes need no escaping. `&` must always be `&amp;`, and `<` must be `&lt;`. [MS: XML character entities and XAML](https://learn.microsoft.com/en-us/dotnet/desktop/xaml-services/xml-character-entities) Playnite's German file shows the cost in practice: `Plattformen &amp; Emulatoren`, and `&quot;` where Crowdin wrote the quotes out. [Playnite `de_DE.xaml`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Localization/de_DE.xaml) Multi-line strings and significant spaces need `xml:space="preserve"`, as the docs' own example shows. A value may begin with a placeholder: Playnite ships `<sys:String x:Key="LOCPlayedDays">{0}d {1}h {2}m</sys:String>`.

**One mistake loses the language.** The file is parsed whole by `XamlReader.Load`; on any exception Playnite logs "Failed to parse localization file" and returns, leaving English. [Playnite `Localization.cs`, `Common/Xaml.cs`] A bare `&` or a deleted `>` therefore drops every string of that language, silently for the user.

**Comments.** XML comments, which `LocSource.xaml` uses only as section headings.

**Plurals.** None. Playnite itself writes `{0} minutes` and `{0}d {1}h {2}m`. [`LocSource.xaml`]

**Reader.** None to write or ship. But Core cannot call `ResourceProvider` (it is Playnite-free), so Core's strings would reach it through a seam the extension implements.

**CI.** An XML well-formedness check catches the mistakes above on any runner; a test can also check each language's keys against English and its `{n}` placeholders. `xmllint` is not on the Ubuntu 24.04 runner image's list. [runner-images: Ubuntu 24.04 readme](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md)

**The catch for this map.** The file name must be one of Playnite's language ids, which the "follows Playnite's language" decision implies for every format anyway.

### JSON, flat

**The format.** `{ "Key": "Value", … }`. Strings must escape the quotation mark, the backslash and control characters; nothing else. So `Who's playing today?`, `&`, `<` and `>` are all written as they are, but a translation that quotes something needs `\"`. The grammar has no comments and no trailing comma, and names "SHOULD be unique". Text must be UTF-8 without a byte order mark, though parsers may ignore one. [RFC 8259](https://www.rfc-editor.org/rfc/rfc8259)

**Hand edits.** The classic slips are a missing comma after a newly added line, a trailing comma after the last one, and an unescaped `"`. Each breaks the whole file for a strict parser.

**Comments.** None in the standard. Json.NET's loader has a `CommentHandling` setting that defaults to ignoring comments [Json.NET: JsonLoadSettings](https://www.newtonsoft.com/json/help/html/T_Newtonsoft_Json_Linq_JsonLoadSettings.htm), but a file with comments is no longer JSON for `jq`, a schema validator or GitHub's highlighter. The standard workarounds put context in the data: Chrome's extension format makes each entry an object with `message`, an optional `description` for translators, and named `placeholders` with examples [Chrome: Localization message formats](https://developer.chrome.com/docs/extensions/how-to/ui/localization-message-formats), which costs the flatness of the file.

**Plurals.** None. A convention is needed, such as `Key.one` / `Key.other` keys, plus plural rules in code (see [Plurals](#plurals-across-formats)).

**Reader.** Newtonsoft.Json 10.0.3, already loaded. A flat file is one `JsonConvert.DeserializeObject<Dictionary<string,string>>` call. **[inference]**

**CI.** `jq` is preinstalled on both runner images; a JSON Schema could be checked with `check-jsonschema` (active, last pushed November 2026). [runner-images readmes](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md), [check-jsonschema](https://github.com/python-jsonschema/check-jsonschema)

### JSON with ICU MessageFormat values

**The format.** JSON as above, with values in ICU's message syntax: `{name}` arguments, and `{count, plural, one {# game} other {# games}}` for plurals, which "does always need an 'other' variant". [ICU User Guide: Formatting Messages](https://unicode-org.github.io/icu/userguide/format_parse/messages/)

**The apostrophe.** ICU uses the ASCII apostrophe to quote syntax characters. Since ICU 4.8, "an ASCII apostrophe only starts quoted text if it immediately precedes a character that requires quoting", so `Who's playing today?` is safe under ICU's own rules; ICU still recommends the real apostrophe `’` (U+2019) for text. [ICU User Guide, as above] The .NET library's README states only that "A pair of single quotes always represents one single quote" and shows `'{` quoting. [jeffijoe/messageformat.net](https://github.com/jeffijoe/messageformat.net) Whether it treats a lone apostrophe as ICU 4.8 does was not tested.

**Hand edits.** Two syntaxes are nested: JSON's quotes and commas, then ICU's braces. A plural message has braces three deep. ICU itself describes messages as something "translators move around the variable elements" of, but in a translation tool, not a raw file.

**Reader.** `MessageFormat` 8.0.0 (jeffijoe): MIT, last released February 2026, with CLDR plural rules generated into the package. It builds for `net8.0`, `net10.0`, `netstandard2.0` and `netstandard2.1` only, so 4.6.2 gets the `netstandard2.0` build: 76 KB plus a `System.Memory` dependency. [NuGet: MessageFormat](https://www.nuget.org/packages/MessageFormat), [its csproj](https://github.com/jeffijoe/messageformat.net/blob/master/src/Jeffijoe.MessageFormat/Jeffijoe.MessageFormat.csproj) `SmartFormat` 3.6.2 is an alternative with a real `net462` build, but 180 KB with `System.Memory`, `System.ValueTuple` and `ZString` behind it. [NuGet: SmartFormat](https://www.nuget.org/packages/SmartFormat) Its message syntax was not read for this note.

### .resx

**The format.** XML: a schema and `resheader` block, then `<data name="…"><value>…</value><comment>…</comment></data>` per string. Microsoft's own description: "must consist of well-defined XML, including a header that must follow a specific schema followed by data in name/value pairs, you may find that creating these files manually is error-prone". [MS: Working with .resx files programmatically](https://learn.microsoft.com/en-us/dotnet/core/extensions/work-with-resx-files-programmatically)

**Hand edits.** The same XML escaping as XAML, with more scaffolding per string and a long header a translator must not disturb.

**Comments.** The best of the XML formats: a `<comment>` element per string.

**The usual route needs a build.** A .resx "cannot" be embedded as it is; it is compiled to `.resources` and into satellite assemblies, and only then does the runtime handle fallback. Read raw at run time, "relying on .resx files at runtime places the responsibility for handling resource fallback on the developer". [MS, as above] The translator still builds nothing either way; the difference is whether the release build compiles their file or the extension reads it as text.

**Reader.** `ResXResourceReader` and `ResXResourceSet`, which on .NET Framework live in System.Windows.Forms.dll, part of the framework. [MS, as above] **[inference]** For string-only files an `XDocument` reader is some 15 lines and avoids loading Windows Forms.

**Plurals.** None.

**Who uses it.** Flow Launcher keeps a `Resources.resx` beside its XAML language files, both fed by Crowdin. [Flow Launcher `crowdin.yml`](https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/crowdin.yml)

### gettext PO

**The format.** Entries of comments, then `msgid` (the source string) and `msgstr` (the translation). [GNU gettext manual, "The Format of PO Files"](https://www.gnu.org/software/gettext/manual/html_node/PO-Files.html), read from its source [`gettext.texi`](https://github.com/autotools-mirror/gettext/blob/master/gettext-tools/doc/gettext.texi)

```po
#. Shown over the profiles when Playnite starts
msgid "Who's playing today?"
msgstr ""
```

**Hand edits.** Strings use `"` delimiters and `\` escapes in C syntax, so apostrophes, `&` and `<` are plain, and a quoted word needs `\"`. The manual expects a tool: "the translator does not really have to pay attention to the precise quoting format, as PO mode fully takes care of quoting for her". A hand editor has no such help, and must leave every `msgid` line alone.

**Fallback is built in.** An empty `msgstr` means untranslated, which is exactly "falls back to English". And the English text sits right above the line being translated, which no other format here gives.

**Keys.** The key is the English text itself, so rewording English orphans its translations. Kodi avoids that with numeric ids in `msgctxt` (`msgctxt "#31000"`), keeping `msgid` as the English wording. [Kodi `strings.po`](https://github.com/xbmc/xbmc/blob/master/addons/resource.language.en_gb/resources/strings.po)

**Comments.** The richest set: translator comments (`# `), comments from the programmer to the translator (`#.`), source references (`#:`) and flags (`#,`, including `fuzzy`).

**Plurals.** Native. The file's header carries the language's rule, for example three forms for Russian-like languages:

```
Plural-Forms: nplurals=3; plural=n%10==1 && n%100!=11 ? 0 : n%10>=2 && n%10<=4 && (n%100<10 || n%100>=20) ? 1 : 2;
```

and entries carry `msgid_plural` with `msgstr[0]`, `msgstr[1]`, `msgstr[2]`. [GNU gettext manual, "Plural forms"] The rule travels with the file, so a new language needs no code change; the translator must copy the right formula and know which index is which.

**Reader.** `Karambolo.PO` 1.13.0 parses PO text directly: MIT, last released March 2026, with real `net40` and `net45` builds of 59 KB. The full edition depends on `Karambolo.Common`; the `Karambolo.PO.Compact` edition has no dependency and still evaluates plural expressions; `Minimal` does not. Parsing returns `result.Success` and `result.Diagnostics`. [adams85/po](https://github.com/adams85/po), [NuGet: Karambolo.PO](https://www.nuget.org/packages/Karambolo.PO) `NGettext` 0.6.7 has `net20` to `net46` builds and no dependency, but its last release was September 2021. [NuGet: NGettext](https://www.nuget.org/packages/NGettext) `GetText.NET` is `netstandard2.0` only. [NuGet: GetText.NET](https://www.nuget.org/packages/GetText.NET)

**CI.** `msgfmt -c` is the reference check, but gettext is not on the Ubuntu 24.04 or Windows 2022 runner lists, so it means an `apt install`. [runner-images readmes]

### Project Fluent

**The format.** `key = value`, with no delimiters round the text: "you can just write it without any delimiters (e.g. quotes)". [Fluent Syntax Guide](https://projectfluent.org/fluent/guide/print.html)

```ftl
# Shown over the profiles when Playnite starts
who-is-playing = Who's playing today?
achievements = { $count ->
    [one] { $count } achievement
   *[other] { $count } achievements
}
```

**Hand edits.** The least syntax of any candidate for plain strings. The guide names what is special: curly braces open a placeable, so a literal one is `{"{"}`; a continuation line must be indented by at least one space, and one that starts with `[`, `*` or `.` must be quoted likewise. The guide gives no escaping rule for quotes, apostrophes, ampersands or angle brackets, because text has none. Identifiers are lowercase with hyphens by Mozilla's convention. [Fluent Syntax Guide], [Firefox: Fluent review guidelines](https://firefox-source-docs.mozilla.org/l10n/fluent/review.html)

**One mistake costs one message.** The spec's error design collects parse errors per entry and keeps unparsable text as a `Junk` entry spanning only that text. [Fluent spec: `errors.md`](https://github.com/projectfluent/fluent/blob/master/spec/errors.md) That document shows it by example and does not state it as a guarantee, and Fluent.Net's behaviour was not tested.

**Comments.** `#` bound to a message, `##` for a group, `###` for the file. Mozilla's guidance: "Comments are fundamental for localizers", and every variable should be explained in one. [Firefox: Fluent review guidelines]

**Plurals.** Native, by CLDR category: "The possible categories are: `zero`, `one`, `two`, `few`, `many`, and `other`", with a required default variant. [Fluent Syntax Guide] The translator adds the variants their language needs; no formula.

**Reader.** `Fluent.Net` 1.0.63, "C# port of fluent.js": Apache-2.0, `netstandard2.0` only, 103 KB with no dependency; last released and last pushed November 2024, not archived, 55 stars. [blushingpenguin/Fluent.Net](https://github.com/blushingpenguin/Fluent.Net), [NuGet: Fluent.Net](https://www.nuget.org/packages/Fluent.Net) `Linguini` is the active alternative (pushed October 2026) but builds for `net6.0`, `net8.0` and `netstandard2.1` only, so it cannot load on .NET Framework. [NuGet: Linguini.Bundle](https://www.nuget.org/packages/Linguini.Bundle) **[inference]** A hand-written reader for all of Fluent is a real parser; one for a subset (plain messages, variables, one level of plural select) is feasible but would be this repo's own dialect.

**CI.** `moz-fluent-linter` (Mozilla, MPL-2.0, pushed June 2026) checks `.ftl` files and runs through pre-commit; it is written for the reference file and even flags a straight apostrophe where a typographic one belongs. [mozilla-l10n/moz-fluent-linter](https://github.com/mozilla-l10n/moz-fluent-linter)

### TOML

**The format.** `key = "value"`, UTF-8 by requirement, `#` comments, and "Defining a key multiple times is invalid". [TOML v1.0.0](https://toml.io/en/v1.0.0)

**Hand edits.** Basic strings escape the quotation mark, the backslash and control characters, as JSON does, but there are no commas or braces to break. The trap is the second string type: "Literal strings are surrounded by single quotes" and "there is no way to write a single quote inside a literal string", so `'Who's playing today?'` is an error where `"Who's playing today?"` is fine. Bare keys allow only `A-Za-z0-9_-`.

**Plurals.** None. Sub-tables could carry categories (`[achievements]` with `one = …`, `other = …`), which stops the file being flat.

**Reader.** `Tommy` 3.1.2 has a `net35` build of 41 KB under MIT, whose only listed dependency is the build-time reference assemblies, but its last release was January 2022 (repo pushed April 2022). `Tomlyn` 2.11.1 is current (November 2026) but its `netstandard2.0` build is 330 KB and pulls in `System.Text.Json`, `System.Collections.Immutable` and `System.Buffers`. [NuGet: Tommy](https://www.nuget.org/packages/Tommy), [NuGet: Tomlyn](https://www.nuget.org/packages/Tomlyn)

**CI.** `taplo` (MIT, pushed July 2026). [tamasfe/taplo](https://github.com/tamasfe/taplo)

### YAML

**The format.** `key: value`, with the value usually unquoted.

**Hand edits.** The surface looks the friendliest and has the most hidden rules. "Plain scalars must not begin with most indicators"; they "must never contain the ': ' and ' #' character combinations"; and tabs must not be used for indentation. Inside single quotes an apostrophe is doubled. [YAML 1.2.2](https://yaml.org/spec/1.2.2/) So `Who's playing today?` is fine bare, but a translation containing `: ` (common in French, and in any "Label: value" string) must be quoted, and **[inference]** a value that starts with a placeholder, `{0} achievements`, reads as the start of a flow mapping and fails.

**Plurals.** None; nested keys by convention.

**Reader.** `YamlDotNet` 18.1.0: MIT, active (pushed November 2026), `net47` and `netstandard2.0` builds of 298 KB, no dependency. [NuGet: YamlDotNet](https://www.nuget.org/packages/YamlDotNet) 4.6.2 would take the `netstandard2.0` build. Playnite reads its own `extension.yaml` and `theme.yaml` manifests, but which YAML library and version it loads was not checked.

**CI.** `yamllint` 1.38.0 is preinstalled on both runner images. [runner-images readmes]

### Java .properties

**The format.** `key = value`, one per line. A comment line starts with `#` or `!`. [Java SE 21: `Properties.load`](https://docs.oracle.com/en/java/javase/21/docs/api/java.base/java/util/Properties.html)

**Hand edits.** Nothing is quoted: "Escapes are not necessary for single and double quotes", and `&`, `<`, `{` are ordinary. The backslash is the one special character: it escapes, continues a line, and before any other character "is silently dropped". Leading whitespace of a value is skipped. A mistake stays on its own line.

**Encoding.** Java's stream loader assumes ISO 8859-1 with `\uXXXX` escapes; its reader-based loader takes whatever encoding the caller opens. [Java SE 21, as above] A reader written here would simply read UTF-8, but that makes the file "properties-like", and tools that expect Latin-1 would mis-read it.

**Plurals.** None; a key suffix by convention (`achievements.one`, `achievements.other`).

**Reader.** No reader ships with .NET. **[inference]** A UTF-8 reader for the subset needed (comments, `key = value`, `\n`) is 30 to 50 lines in Core. `Kajabity.Tools.Java` 0.4.0 exists (`net48` and `netstandard2.0`, 13 KB, released January 2026). [NuGet: Kajabity.Tools.Java](https://www.nuget.org/packages/Kajabity.Tools.Java)

**CI.** No standard linter worth adding; the repo's own test is the check.

## Plurals across formats

CLDR's cardinal categories show what "more than two forms" means: English and German have `one` and `other`; French, Spanish and Portuguese add `many`; Russian, Polish, Czech and Lithuanian have `one`, `few`, `many`, `other`; Slovenian has `one`, `two`, `few`, `other`; Arabic has all six; Japanese, Chinese and Korean have `other` alone. [CLDR: Language Plural Rules](https://www.unicode.org/cldr/charts/latest/supplemental/language_plural_rules.html)

Three formats carry plurals themselves, in three different ways:

- **PO** puts the rule in each language's file as a C-like formula, and the forms as numbered `msgstr[n]`. No code knows any language.
- **Fluent** and **ICU MessageFormat** name the CLDR category in the message, and the library supplies each language's rule.

Every other format needs a convention on top (a key per category) and the rules in this repo's code. **[inference]** .NET Framework has no plural-rule API, so the rules would be a hand-kept table, and a language missing from the table would get `other` for every count until someone edits C#. That cuts against "anyone can add a language".

How much this matters depends on how many of the Bundle's strings depend on a count. That inventory is not part of this ticket; a quick look found count-free forms such as `"{span.TotalMinutes:0} min"` in Core. Playnite itself ships with no plural support at all.

## How the Dashboard's XAML gets a string

- **Playnite XAML**: `{DynamicResource Key}`, loaded by Playnite from the theme's or the extension's `Localization` folder. This is the only route that works with the theme installed alone.
- **Every other format**: **[inference]** the extension reads the file as Playnite starts and adds a `ResourceDictionary` of strings to `Application.Current.Resources`, which is exactly what Playnite's own loader does with a XAML file. The Dashboard then uses the same `{DynamicResource Key}`. This is untested, including whether the extension loads early enough for the first frame. Without the extension the theme has no strings, unless the build also generates an `en_US.xaml` from the English file.
- **Strings with values** (`{0}` or `{ $count }`) can't be filled by `{DynamicResource}` alone in any format; XAML needs `StringFormat`, a `MultiBinding`, or the extension to format the text.

## What similar projects use

| Project | Format | How a translation arrives |
| --- | --- | --- |
| **Playnite** | XAML resource dictionaries | Crowdin only: "We use Crowdin to manage localization, please join our project". PRs are taken for the English source file alone. [Playnite README](https://github.com/JosefNemec/Playnite#translations), [`crowdin.yml`](https://github.com/JosefNemec/Playnite/blob/master/crowdin.yml) |
| **Playnite add-ons** | The same XAML, in `Localization/` | By each author's choice; the docs only fix the file layout. [Playnite docs] |
| **Flow Launcher** (WPF) | XAML resource dictionaries (`Languages/en.xaml`, 27 files) plus a .resx | Crowdin. [Flow Launcher `Languages`](https://github.com/Flow-Launcher/Flow.Launcher/tree/dev/Flow.Launcher/Languages), [`crowdin.yml`](https://github.com/Flow-Launcher/Flow.Launcher/blob/dev/crowdin.yml) |
| **Kodi** and its skins | gettext PO with numeric `msgctxt` ids, one folder per language (`resource.language.en_gb/strings.po`) | A translation platform; the file header still names Transifex. [Kodi `strings.po`](https://github.com/xbmc/xbmc/blob/master/addons/resource.language.en_gb/resources/strings.po), [Estuary's `language` folder](https://github.com/xbmc/xbmc/tree/master/addons/skin.estuary/language) |
| **Jellyfin web** | Flat JSON with `{0}` placeholders (`src/strings/en-us.json`) | Weblate: "we use Weblate running at translate.jellyfin.org". [Jellyfin: Contributing](https://jellyfin.org/docs/general/contributing/), [`en-us.json`](https://github.com/jellyfin/jellyfin-web/blob/master/src/strings/en-us.json) |
| **Firefox** | Fluent | Mozilla's own localisation tooling, with a linter in the build and reviewers on every `.ftl` change. [Firefox: Fluent review guidelines](https://firefox-source-docs.mozilla.org/l10n/fluent/review.html) |
| **Chrome extensions** | JSON objects with `message`, `description`, `placeholders` | Up to each extension. [Chrome docs](https://developer.chrome.com/docs/extensions/how-to/ui/localization-message-formats) |

Every project here that publishes its flow puts a translation platform in front of the files; none asks translators to edit the raw file. The map rules a platform out for now, so the raw file's forgiveness carries the weight a platform normally carries. The formats Crowdin and Weblate read (XAML, JSON, PO, .resx, Fluent, .properties among them) keep that door open later; this was not checked against either platform's format list.

## Trade-offs

**Least to write and ship: Playnite XAML.** Playnite loads it, falls back per key, and the theme works alone. It is what Playnite's translators already know from other add-ons. Against it: XML's `&` and `<`, a wall of angle brackets round each string, one slip losing the whole language, and no plurals.

**Least syntax for the translator: Fluent and .properties.** Both are `key = text` with no quotes, and a mistake costs one string. Fluent adds plurals and comments tied to messages, at the price of a `netstandard2.0`-only library last touched in November 2024 or a home-made subset. .properties has no reader on .NET, so the repo writes one, small; it has no plurals and its "official" encoding is not UTF-8.

**Familiar and free to read: flat JSON.** The reader is already loaded and `jq` is already on the runners. Against it: commas and quote escaping break the whole file, no comments without leaving the standard, no plurals.

**Plurals with the rule in the file: PO.** The best comments, the English shown beside each translation, fallback by empty `msgstr`, and a maintained reader with true .NET Framework builds. Against it: the densest file to hand-edit, a header block to carry, and plural formulas translators must copy correctly.

**Plurals by category: ICU in JSON, or Fluent.** Both need a `netstandard2.0` library on 4.6.2. ICU in JSON stacks two syntaxes.

**Weakest fits.** .resx has the heaviest scaffolding and Microsoft itself calls hand-writing it error-prone. YAML's bare values fail on `: `, ` #` and a leading `{`, which are all likely in UI strings. TOML has no advantage over JSON large enough to offset an unmaintained small reader or a heavy current one, and its single-quote strings trip on the test sentence.

**Common to all.** Whatever the format, the strongest CI check is the repo's own: a Core test that loads every language file with the real reader and compares keys and placeholders with English. External linters add little beyond that. And any format but Playnite's XAML needs the build to generate an `en_US.xaml` if the theme is to keep its text without the extension.

## What could not be verified

- **Nothing was run.** No .NET SDK on this machine: no library was loaded on 4.6.2, and no sample file was parsed by any reader.
- **Extension-loaded strings reaching the theme's XAML**, and whether a dictionary the extension adds at start-up is there before the Dashboard's first frame. Both follow from Playnite's source but were not tried.
- **`netstandard2.0` libraries inside Playnite** (MessageFormat with `System.Memory`, Fluent.Net, YamlDotNet): whether they load cleanly on 4.6.2 beside other add-ons' assemblies.
- **Newtonsoft.Json 10.0.3's leniency**: the comment-ignoring default is documented for the current version; whether 10.0.3 has it, and whether it accepts a trailing comma, was not checked.
- **messageformat.net and a lone apostrophe**, as in `Who's`: the README shows only the quoting cases.
- **Fluent's per-message recovery in Fluent.Net**, and Fluent.Net's coverage of CLDR plural rules. The Fluent spec shows `Junk` recovery by example only; the spec's own `recovery.md` was not found.
- **NGettext reading `.po` text** rather than compiled `.mo` files: its README was not read.
- **Which YAML library Playnite loads**, and so whether YAML could be read with no new DLL.
- **GitHub's web editor and encoding**: whether it keeps a UTF-8 byte order mark (Playnite's XAML files start with one) and existing line endings. GitHub's docs don't say.
- **A XAML value that starts with `{`**: Playnite ships one in element content, so it works there; Microsoft's page documents the `{}` escape for attribute values only.
- **Kodi's current platform and contributor steps**: the Kodi wiki refused the fetch. **Jellyfin's** docs don't say whether direct PRs to the JSON files are accepted.
- **Crowdin's and Weblate's supported formats**, for a later move to a platform.
- **Which of the Bundle's strings depend on a count.** Not inventoried here.
- **.NET Framework having no plural-rule API**: stated from knowledge, not from a source.
