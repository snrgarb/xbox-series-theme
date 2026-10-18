using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace XboxSeries.Core.Tests
{
    // Reading fonts out of the theme's and the extension's XAML for the #77 checks: which shared font a style reaches,
    // following its BasedOn, and where a font is set other than through the shared resources.
    internal static class XamlFonts
    {
        public static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";
        public static readonly string RepoRoot = FindRepoRoot();
        public static readonly string ConstantsPath = Path.Combine(RepoRoot, "src", "XboxSeries.Dashboard", "Constants.xaml");

        // The shared fonts, named once in Constants.xaml.
        public static readonly string[] SharedKeys =
            { "XboxFont", "XboxFontSemibold", "XboxHeaderFont", "XboxHeaderFontSemibold", "XboxIconFont" };

        public static XElement StyleByKey(XDocument doc, string key) =>
            doc.Descendants().Single(e => e.Name.LocalName == "Style" && (string)e.Attribute(X + "Key") == key);

        // The font key a style reaches: its own FontFamily setter, else its BasedOn's, all the way down; null for none.
        public static string FontOfStyle(XDocument doc, XElement style)
        {
            var setter = style.Elements().FirstOrDefault(e => e.Name.LocalName == "Setter" && (string)e.Attribute("Property") == "FontFamily");
            if (setter != null)
            {
                return ResourceKey((string)setter.Attribute("Value"));
            }

            var basedOn = ResourceKey((string)style.Attribute("BasedOn"));
            return basedOn == null ? null : FontOfStyle(doc, StyleByKey(doc, basedOn));
        }

        public static string FontOfStyle(XDocument doc, string key) => FontOfStyle(doc, StyleByKey(doc, key));

        public static string ResourceKey(string markup)
        {
            var match = markup == null ? null : Regex.Match(markup, @"^\{(?:Dynamic|Static)Resource\s+(\w+)\}$");
            return match != null && match.Success ? match.Groups[1].Value : null;
        }

        // Every font a document sets other than by a resource reference to one of the shared fonts: a FontFamily (or
        // HtmlFontFamily) attribute, a <X.FontFamily> property element, a FontFamily setter whichever way it's written,
        // or a FontFamily resource of its own.
        public static IEnumerable<string> FontsNotShared(XDocument doc)
        {
            foreach (var e in doc.Descendants())
            {
                if (e.Name.LocalName == "FontFamily")
                {
                    yield return $"<FontFamily> {e.Value.Trim()}";
                }
                else if (e.Name.LocalName.EndsWith(".FontFamily"))
                {
                    yield return $"<{e.Name.LocalName}>";
                }
                else if (e.Name.LocalName == "Setter" && (string)e.Attribute("Property") is string p && p.EndsWith("FontFamily"))
                {
                    var value = (string)e.Attribute("Value");
                    if (value == null || !IsShared(value))
                    {
                        yield return $"Setter {p}={value ?? "<Setter.Value>"}";
                    }
                }

                foreach (var a in e.Attributes().Where(a => a.Name.LocalName.EndsWith("FontFamily") && !IsShared(a.Value)))
                {
                    yield return $"{a.Name.LocalName}={a.Value}";
                }
            }
        }

        private static bool IsShared(string markup) => SharedKeys.Contains(ResourceKey(markup));

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
        }
    }
}
