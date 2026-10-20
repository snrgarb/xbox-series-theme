using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;
using static XboxSeries.Core.Tests.XamlFonts;

namespace XboxSeries.Core.Tests
{
    // The view's side of the Idle screen's dim. The window isn't run here (it was shown over a harness's windows with
    // the core's timers, and woken through IdleInput as Playnite routes the pad); these checks guard what it is: a slight
    // black over the whole screen that never takes focus, the foreground or the mouse, above the Overlay and the Launch
    // screen's and beneath the Toast's.
    public class IdleDimXamlTests
    {
        private static readonly string ExtensionDir = Path.Combine(RepoRoot, "src", "XboxSeries.Extension");

        private static XElement Dim() => XDocument.Load(Path.Combine(ExtensionDir, "DimWindow.xaml")).Root;

        private static string Code(string file) => File.ReadAllText(Path.Combine(ExtensionDir, file));

        [Fact]
        public void The_dim_is_a_70_percent_black_over_the_whole_window_starting_clear()
        {
            var shade = Dim().Descendants().Single(e => (string)e.Attribute(X + "Name") == "Shade");
            var color = (string)shade.Attribute("Background");
            Assert.Matches("^#[0-9A-Fa-f]{8}$", color);
            Assert.Equal("000000", color.Substring(3));
            var alpha = int.Parse(color.Substring(1, 2), NumberStyles.HexNumber) / 255.0;
            Assert.InRange(alpha, 0.68, 0.72);
            Assert.Equal("0", (string)shade.Attribute("Opacity"));
            Assert.Null(shade.Attribute("Width"));
            Assert.Null(shade.Attribute("Height"));
            Assert.Null(shade.Attribute("Margin"));
        }

        [Fact]
        public void The_dim_never_takes_focus_the_foreground_or_the_mouse()
        {
            var window = Dim();
            Assert.Equal("False", (string)window.Attribute("ShowActivated"));
            Assert.Equal("False", (string)window.Attribute("Focusable"));
            Assert.Equal("False", (string)window.Attribute("IsHitTestVisible"));
            Assert.Equal("False", (string)window.Attribute("ShowInTaskbar"));
            Assert.Equal("True", (string)window.Attribute("Topmost"));

            // Not a layered window copied back through the processor every frame at 4K: see-through as the Toast is, and
            // layered at full alpha only so clicks pass through it.
            Assert.Equal("False", (string)window.Attribute("AllowsTransparency"));
            var code = Code("DimWindow.xaml.cs");
            Assert.Contains("NativeMethods.MakeClickThrough(handle)", code);
            Assert.Contains("NativeMethods.SeeThrough(", code);

            // Placed and raised without activating, every time.
            var placements = Regex.Matches(code, @"SetWindowPos\((?:[^;])*;").Cast<Match>().Select(m => m.Value).ToList();
            Assert.NotEmpty(placements);
            Assert.All(placements, p => Assert.Contains("SWP_NOACTIVATE", p));
            Assert.DoesNotContain("Activate()", code);
            Assert.DoesNotContain("Focus()", code);
        }

        [Fact]
        public void The_dim_fades_in_slowly_and_out_quickly_and_hides_rather_than_closing_what_is_under_it()
        {
            var code = Code("DimWindow.xaml.cs");
            var inMs = int.Parse(Regex.Match(code, @"FadeInDuration = TimeSpan\.FromMilliseconds\((\d+)\)").Groups[1].Value);
            var outMs = int.Parse(Regex.Match(code, @"FadeOutDuration = TimeSpan\.FromMilliseconds\((\d+)\)").Groups[1].Value);
            Assert.InRange(inMs, 500, 1000);
            Assert.InRange(outMs, 100, inMs - 1);
            Assert.DoesNotContain(".Close()", code);
        }

        [Fact]
        public void The_dim_is_made_over_the_Overlay_and_the_Launch_screen_and_beneath_the_Toast()
        {
            var plugin = Code("XboxSeriesPlugin.cs");
            var made = Regex.Match(plugin, @"new DimWindow\(core\.Idle, core\.Status, new Window\[\] \{ (?<below>[^}]*) \}, (?<above>[^)]*)\)");
            Assert.True(made.Success, "The plugin makes the dim with the windows below it and above it.");
            Assert.Equal(new[] { "overlay", "launchScreenWindow" }, made.Groups["below"].Value.Split(',').Select(s => s.Trim()));
            Assert.Equal("toastWindow", made.Groups["above"].Value.Trim());

            // After the windows it names, so none is null.
            Assert.True(plugin.IndexOf("new DimWindow(", StringComparison.Ordinal) > plugin.IndexOf("launchScreenWindow = launchScreenWindow ??", StringComparison.Ordinal));
        }

        [Fact]
        public void Every_input_is_heard_before_WPF_routes_it_and_the_wake_press_cancelled_there()
        {
            var code = Code("IdleInput.cs");
            Assert.Contains("InputManager.Current.PreProcessInput += OnPreProcessInput", code);
            Assert.Contains("InputManager.Current.PreProcessInput -= OnPreProcessInput", code);
            Assert.Contains("e.Cancel()", code);

            // The pad's buttons come as KeyDown alone, never PreviewKeyDown; real keys as PreviewKeyDown first.
            Assert.Contains("routed == Keyboard.KeyDownEvent", code);
            Assert.Contains("PlayniteInput.PadButtonOf(", code);
            Assert.Contains("routed == Keyboard.PreviewKeyDownEvent", code);
            Assert.Contains("routed == Mouse.PreviewMouseDownEvent", code);
            Assert.Contains("routed == Mouse.PreviewMouseWheelEvent", code);
            Assert.Contains("routed == Mouse.PreviewMouseMoveEvent", code);
        }
    }
}
