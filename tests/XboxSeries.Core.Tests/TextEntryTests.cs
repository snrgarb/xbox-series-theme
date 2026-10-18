using System;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>Text entry over the page: every text the Bundle asks for, typed on the console keyboard.</summary>
    public class TextEntryTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 18, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSecretStore secrets = new FakeSecretStore();
        private readonly FakePlayniteHost playnite;
        private readonly ExtensionCore core;

        public TextEntryTests()
        {
            playnite = new FakePlayniteHost(windows);
            core = new ExtensionCore(clock, windows, playnite, new FakeSoundPlayer(), new FakeDrives(),
                new FakeUserAccount { Name = "gabe", RealName = "Gabe Smith" }, new FakeAudio(), new FakeController(),
                new FakeDiscordTransport(), new FakeDiscordOAuth(), secrets, new FakeDownloads());
        }

        [Fact]
        public void Add_profile_opens_text_entry_empty_for_a_gamertag()
        {
            core.OpenSettings();

            core.TypeNewProfile();

            Assert.True(core.TextEntry.IsShown);
            Assert.Equal("Gamertag", core.TextEntry.Caption);
            Assert.False(core.TextEntry.IsSecret);
            Assert.Equal(string.Empty, Keyboard.Text);
            Assert.True(Keyboard.IsShifted);
            Assert.False(Keyboard.ShowsSymbols);
        }

        [Fact]
        public void A_new_profiles_gamertag_kept_adds_the_profile()
        {
            core.OpenSettings();
            core.TypeNewProfile();

            Type("Ana");
            core.ConfirmTextEntry();

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe", "Ana" }, Gamertags());
        }

        [Fact]
        public void The_keyboards_keys_type_as_Searchs_do_and_Enter_keeps_the_text()
        {
            core.OpenSettings();
            core.TypeNewProfile();

            Press(Key("B"));
            Press(Key("o"));
            Press(Key(KeyboardKeyKind.Space));
            Press(Key(KeyboardKeyKind.Symbols));
            Press(Key("!"));
            Press(Key(KeyboardKeyKind.Backspace));
            Press(Key(KeyboardKeyKind.Left));
            Press(Key(KeyboardKeyKind.Backspace));
            Assert.Equal("B ", Keyboard.Text);
            Press(Key(KeyboardKeyKind.Enter));

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe", "B" }, Gamertags());
        }

        [Fact]
        public void The_pads_shortcuts_edit_the_text_and_Start_keeps_it()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Bob");

            Assert.True(core.PressKeyboardShortcut(ControllerButton.X));
            Assert.True(core.PressKeyboardShortcut(ControllerButton.LeftShoulder));
            Assert.True(core.PressKeyboardShortcut(ControllerButton.Y));
            Assert.True(core.PressKeyboardShortcut(ControllerButton.RightShoulder));
            Assert.True(core.PressKeyboardShortcut(ControllerButton.LeftStick));
            Assert.False(core.PressKeyboardShortcut(ControllerButton.A));
            Press(Key("Z"));
            Assert.Equal("B oZ", Keyboard.Text);
            Assert.True(core.PressKeyboardShortcut(ControllerButton.Start));

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe", "B oZ" }, Gamertags());
        }

        [Fact]
        public void A_physical_keyboards_Backspace_deletes_before_the_cursor()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Anna");

            core.DeleteText();

            Assert.Equal("Ann", Keyboard.Text);
        }

        [Fact]
        public void Cancelling_a_new_profiles_gamertag_adds_nothing()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Ana");

            core.CancelTextEntry();

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe" }, Gamertags());
        }

        [Fact]
        public void A_blank_gamertag_kept_adds_nothing()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("  ");

            core.ConfirmTextEntry();

            Assert.Equal(new[] { "gabe" }, Gamertags());
        }

        [Fact]
        public void Text_entry_opens_only_over_Settings()
        {
            core.TypeNewProfile();

            Assert.False(core.TextEntry.IsShown);
        }

        [Fact]
        public void Nothing_else_opens_while_text_entry_is_up()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Ana");

            core.TypeDiscordClientSecret();

            Assert.Equal("Gamertag", core.TextEntry.Caption);
            Assert.Equal("Ana", Keyboard.Text);
        }

        [Fact]
        public void A_profiles_Gamertag_box_types_from_its_gamertag_the_cursor_at_its_end()
        {
            core.OpenSettings();

            core.TypeProfileGamertag(First.Id);

            Assert.Equal("Gamertag", core.TextEntry.Caption);
            Assert.Equal("gabe", Keyboard.Text);
            Assert.Equal(4, Keyboard.Cursor);
            Assert.False(Keyboard.IsShifted);
        }

        [Fact]
        public void A_profiles_gamertag_kept_renames_it()
        {
            core.OpenSettings();
            core.TypeProfileGamertag(First.Id);

            Type("_x");
            core.ConfirmTextEntry();

            Assert.Equal(new[] { "gabe_x" }, Gamertags());
        }

        [Fact]
        public void A_profiles_Real_name_box_types_from_its_real_name_and_keeps_it()
        {
            core.OpenSettings();
            core.TypeProfileRealName(First.Id);
            Assert.Equal("Real name", core.TextEntry.Caption);
            Assert.Equal("Gabe Smith", Keyboard.Text);

            core.DeleteText();
            core.ConfirmTextEntry();

            Assert.Equal("Gabe Smit", First.RealName);
        }

        [Fact]
        public void An_unknown_profile_opens_nothing()
        {
            core.OpenSettings();

            core.TypeProfileGamertag("nobody");
            core.TypeProfileRealName("nobody");

            Assert.False(core.TextEntry.IsShown);
        }

        [Fact]
        public void Discords_Client_ID_types_from_what_is_saved_and_saves_once_kept()
        {
            core.SetDiscordClientId("123");
            core.OpenSettings();

            core.TypeDiscordClientId();
            Assert.Equal("Client ID", core.TextEntry.Caption);
            Assert.False(core.TextEntry.IsSecret);
            Assert.Equal("123", Keyboard.Text);
            Type("45");
            core.ConfirmTextEntry();

            Assert.Equal("12345", core.Settings.Discord.ClientId);
            Assert.Equal("12345", secrets.Read("Discord.ClientId"));
        }

        [Fact]
        public void Discords_Client_Secret_types_from_empty_as_dots_and_saves_once_kept()
        {
            core.SetDiscordClientSecret("old");
            core.OpenSettings();

            core.TypeDiscordClientSecret();
            Assert.Equal("Client Secret", core.TextEntry.Caption);
            Assert.True(core.TextEntry.IsSecret);
            Assert.Equal(string.Empty, Keyboard.Text);
            Type("new-secret");
            core.ConfirmTextEntry();

            Assert.Equal("new-secret", secrets.Read("Discord.ClientSecret"));
        }

        [Fact]
        public void Keeping_an_empty_Client_Secret_leaves_the_saved_one()
        {
            core.SetDiscordClientSecret("old");
            core.OpenSettings();
            core.TypeDiscordClientSecret();

            core.ConfirmTextEntry();

            Assert.Equal("old", secrets.Read("Discord.ClientSecret"));
            Assert.True(core.Settings.Discord.HasClientSecret);
        }

        [Fact]
        public void A_secret_after_it_leaves_the_next_entry_plain()
        {
            core.OpenSettings();
            core.TypeDiscordClientSecret();
            core.CancelTextEntry();

            core.TypeDiscordClientId();

            Assert.False(core.TextEntry.IsSecret);
        }

        [Fact]
        public void B_on_Settings_under_text_entry_leaves_the_page_where_it_is()
        {
            core.OpenSettings();
            core.TypeNewProfile();

            core.LeaveSettings();

            Assert.True(core.Settings.IsShown);
            Assert.True(core.TextEntry.IsShown);
        }

        [Fact]
        public void B_on_Full_library_under_text_entry_leaves_the_page_and_its_name_where_they_are()
        {
            WithLibrary();
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.TypeFullLibraryName();
            Type("ha");

            core.LeaveMyGames();

            Assert.True(core.MyGames.ShowsFullLibrary);
            Assert.Equal("ha", core.MyGames.NameFilter);
            Assert.True(core.TextEntry.IsShown);
        }

        [Fact]
        public void The_pads_Start_never_reaches_a_tile_focused_under_text_entry()
        {
            FocusedTileUnderTextEntry();

            Press(ControllerButton.Start);

            Assert.Null(core.Home.TileMenu);
            Assert.True(core.TextEntry.IsShown);
        }

        [Fact]
        public void The_pads_A_never_reaches_a_tile_focused_under_text_entry()
        {
            var tile = FocusedTileUnderTextEntry();

            Press(ControllerButton.A);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Empty(playnite.OpenedHubs);
            Assert.True(core.TextEntry.IsShown);
        }

        [Fact]
        public void A_tile_chosen_under_text_entry_does_nothing()
        {
            var tile = FocusedTileUnderTextEntry();

            core.ChooseHomeTile(tile);
            clock.Advance(TimeSpan.FromSeconds(2));

            Assert.Empty(playnite.OpenedHubs);
        }

        [Fact]
        public void The_Guide_opens_over_text_entry_and_has_the_pad_until_it_goes()
        {
            core.OpenSettings();
            core.TypeNewProfile();

            Tap(ControllerButton.Guide);
            Assert.True(core.Guide.IsOpen);
            Type("Ana");
            Assert.False(core.PressKeyboardShortcut(ControllerButton.Start));
            core.CancelTextEntry();

            Assert.Equal(string.Empty, Keyboard.Text);
            Assert.True(core.TextEntry.IsShown);
        }

        [Fact]
        public void Settings_closing_under_text_entry_drops_the_text()
        {
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Ana");

            core.OpenMyGames(MyGamesItem.Games);

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe" }, Gamertags());
        }

        [Fact]
        public void Full_library_going_under_text_entry_drops_the_text_and_the_name()
        {
            WithLibrary();
            core.OpenMyGames(MyGamesItem.FullLibrary);
            core.TypeFullLibraryName();
            Type("ha");

            core.OpenMyGames(MyGamesItem.Games);

            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(string.Empty, core.MyGames.NameFilter);
            Assert.Equal(string.Empty, playnite.MainListName);
        }

        [Fact]
        public void A_game_exiting_back_to_Home_drops_the_text()
        {
            WithLibrary();
            var hades = core.Home.RecentRow.Single(t => t.Title == "Hades").GameId;
            Assert.True(core.OnGameStarting(hades));
            core.OnGameStarted(hades);
            core.OpenSettings();
            core.TypeNewProfile();
            Type("Ana");

            core.OnGameStopped(hades);

            Assert.False(core.Settings.IsShown);
            Assert.False(core.TextEntry.IsShown);
            Assert.Equal(new[] { "gabe" }, Gamertags());
        }

        /// <summary>Full library, focus on a game's tile, as Y opens text entry over it.</summary>
        private HomeTile FocusedTileUnderTextEntry()
        {
            WithLibrary();
            core.OpenMyGames(MyGamesItem.FullLibrary);
            var tile = core.Home.RecentRow.Single(t => t.Title == "Hades");
            core.FocusHomeTile(tile);
            core.TypeFullLibraryName();
            return tile;
        }

        /// <summary>A library of two played games, in Playnite and the core.</summary>
        private void WithLibrary()
        {
            playnite.Library = new FakeLibrary()
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 13))
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 1))
                .Snapshot();
            core.OnLibraryChanged(playnite.Library);
        }

        /// <summary>Pressed and let go. A Guide tap opens the Guide 0.3 s after its press, so it waits that out.</summary>
        private void Tap(ControllerButton button)
        {
            Press(button);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }

        private Profile First => core.Settings.StartUp.Profiles[0];

        private ConsoleKeyboard Keyboard => core.TextEntry.Keyboard;

        private string[] Gamertags() => core.Settings.StartUp.Profiles.Select(p => p.Gamertag).ToArray();

        private void Press(ControllerButton button) => core.OnControllerButton(button, ButtonState.Pressed);

        /// <summary>The keyboard's key of <paramref name="kind"/>, of which there is one but for Character keys.</summary>
        private KeyboardKey Key(KeyboardKeyKind kind) => Keyboard.Keys.Single(k => k.Kind == kind);

        /// <summary>The Character key reading <paramref name="text"/> now.</summary>
        private KeyboardKey Key(string text) => Keyboard.Keys.Single(k => k.Kind == KeyboardKeyKind.Character && k.Text == text);

        private void Press(KeyboardKey key) => core.PressKey(key);

        /// <summary>Types on a physical keyboard.</summary>
        private void Type(string text) => core.TypeText(text);
    }
}
