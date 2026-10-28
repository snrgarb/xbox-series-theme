using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Playnite binds real keys on its view host to the same commands as its pad buttons: Y its keyboard for the name,
    /// F its filters, F2 and F3 the filter presets. The pages that stop those pad buttons stop their keys too, so a
    /// keyboard's Y on My games &amp; apps opens Search rather than Playnite's keyboard.
    /// </summary>
    public class PlayniteKeysTests
    {
        private static readonly string ExtensionDir = Path.Combine(FindRepoRoot(), "extension", "XboxSeries.Extension");

        [Fact]
        public void Playnites_keys_read_as_the_pad_buttons_bound_to_the_same_commands()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "PlayniteInput.cs"));
            var method = Regex.Match(code, @"ControllerInput\? PlayniteButtonOf\(KeyEventArgs e\).*?\n        \}", RegexOptions.Singleline).Value;

            Assert.Contains("PadButtonOf(e)", method);
            Assert.Contains("ModifierKeys.None", method);
            Assert.Matches(@"case Key\.Y: return ControllerInput\.Y;", method);
            Assert.Matches(@"case Key\.F: return ControllerInput\.RightStick;", method);
            Assert.Matches(@"case Key\.F2: return ControllerInput\.LeftShoulder;", method);
            Assert.Matches(@"case Key\.F3: return ControllerInput\.RightShoulder;", method);
        }

        [Theory]
        [InlineData("OnPageKeyDown")]
        [InlineData("OnMainListKeyDown")]
        [InlineData("OnTabsKeyDown")]
        [InlineData("OnLibraryBarKeyDown")]
        public void My_games_stops_Playnites_keys_as_it_does_its_pad_buttons(string handler)
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "MyGamesControl.xaml.cs"));
            var body = Regex.Match(code, @"void " + handler + @"\(object sender, KeyEventArgs e\)\s*\{.*?\n        \}", RegexOptions.Singleline).Value;

            Assert.Contains("PlayniteButtonOf(e)", body);
            Assert.DoesNotContain("PlayniteInput.PadButtonOf(e)", body);
        }

        [Fact]
        public void The_name_box_keeps_the_letters_typed_in_it()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "MyGamesControl.xaml.cs"));

            Assert.Matches(new Regex(@"ControllerInput\? PlayniteButtonOf\(KeyEventArgs e\) =>\s*e\.OriginalSource == NameBox \? PlayniteInput\.PadButtonOf\(e\) : PlayniteInput\.PlayniteButtonOf\(e\);"), code);
        }

        [Fact]
        public void Settings_stops_Playnites_keys_as_it_does_its_pad_buttons()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "SettingsControl.xaml.cs"));
            var body = Regex.Match(code, @"void OnPageKeyDown\(object sender, KeyEventArgs e\)\s*\{.*?\n        \}", RegexOptions.Singleline).Value;

            Assert.Contains("PlayniteInput.PlayniteButtonOf(e)", body);
        }

        [Fact]
        public void Playnites_view_host_opens_Search_on_Y_and_none_of_its_own_filters_or_presets()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "PlayniteViewHost.cs"));

            Assert.Matches(@"binding\.Command == search\)\s*\{\s*binding\.Command = new RelayCommand\(core\.SearchFromHome\);", code);
            Assert.Matches(@"binding\.Command == filters \|\| binding\.Command == presetPicker\)\s*\{\s*viewHost\.InputBindings\.Remove\(binding\);", code);
            Assert.Matches(@"binding\.Command == previousPreset \|\| binding\.Command == nextPreset\)\s*\{\s*binding\.Command = FullLibraryOnly\(binding\.Command, core\);", code);
            Assert.Contains("() => core.MyGames.ShowsFullLibrary && command.CanExecute(null)", code);
            Assert.Contains("FullscreenCommand(\"OpenSearchCommand\")", code);
            Assert.Contains("FullscreenCommand(\"ToggleFiltersCommand\")", code);
            Assert.Contains("FullscreenCommand(\"SelectFilterPresetCommand\")", code);
            Assert.Contains("FullscreenCommand(\"PrevFilterViewCommand\")", code);
            Assert.Contains("FullscreenCommand(\"NextFilterViewCommand\")", code);
        }

        [Fact]
        public void Search_takes_over_Playnites_view_host_as_it_loads()
        {
            var code = File.ReadAllText(Path.Combine(ExtensionDir, "SearchControl.xaml.cs"));

            Assert.Matches(@"viewHost = HomeRows\.FindPart\(this, ""PART_ViewHost""\);\s*if \(viewHost != null\)\s*\{\s*PlayniteViewHost\.TakeOver\(viewHost, core\);", code);
        }

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
