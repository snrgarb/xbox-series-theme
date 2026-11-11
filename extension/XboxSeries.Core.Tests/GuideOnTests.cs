using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// Settings' Guide checkbox, on Theme settings' Guide page: whether the Guide is on. On until set, in force at once,
    /// saved. Off, the Guide never shows: the Guide button does nothing, a hold neither; Home's profile and Achievements
    /// tile do nothing, its Discord tile opens Settings' Discord page; Toasts drop "Press ⓧ to view"; the Idle screen
    /// leaves out its Guide tips; and a prompt that would show over the Guide shows on its own.
    /// </summary>
    public class GuideOnTests : DiscordTestsBase
    {
        private GuideSettingsState Options => core.Settings.Guide;

        /// <summary>Halo Infinite's id, once <see cref="StartHaloWithHadesBeside"/> has started it.</summary>
        private Guid haloId;

        private void TurnOff() => core.SetGuideOn(false);

        [Fact]
        public void With_nothing_saved_the_Guide_is_on_and_nothing_of_it_goes_to_the_store()
        {
            Assert.True(GuideSettingsState.GuideOnByDefault);
            Assert.True(Options.IsGuideOn);

            core.SetNowPlayingSource("Any");

            Assert.DoesNotContain("IsGuideOn", secrets.Values["Guide"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void It_is_saved_at_once_beside_the_Now_Playing_source_and_kept_across_a_restart(bool on)
        {
            core.SetNowPlayingSource("Any");
            core.SetGuideOn(on);

            Assert.Equal(on, Options.IsGuideOn);
            Assert.Contains(on ? "\"IsGuideOn\":true" : "\"IsGuideOn\":false", secrets.Values["Guide"]);

            NewCore();

            Assert.Equal(on, Options.IsGuideOn);
            Assert.Equal("Any", Options.NowPlayingSource);
        }

        [Fact]
        public void Off_a_tap_of_the_Guide_button_opens_nothing()
        {
            TurnOff();

            OpenGuide();

            Assert.False(Guide.IsOpen);
            Assert.False(Guide.IsOverlayShown);
        }

        [Fact]
        public void Off_a_hold_of_the_Guide_button_opens_no_Power()
        {
            TurnOff();

            core.OnControllerButton(ControllerButton.Guide, ButtonState.Pressed);
            clock.Advance(TimeSpan.FromSeconds(1));
            core.OnControllerButton(ControllerButton.Guide, ButtonState.Released);

            Assert.False(Guide.IsOverlayShown);
            Assert.Null(Guide.Dialog);
        }

        [Fact]
        public void Turned_on_again_the_Guide_button_opens_it_at_once_without_a_restart()
        {
            TurnOff();
            core.SetGuideOn(true);

            OpenGuide();

            Assert.True(Guide.IsOpen);
        }

        [Fact]
        public void Off_Homes_profile_and_Achievements_tile_open_nothing()
        {
            TurnOff();

            core.OpenProfile();

            Assert.False(Guide.IsOverlayShown);
        }

        [Fact]
        public void Off_Homes_Discord_tile_opens_Settings_on_Discord_even_with_a_Discord_tab()
        {
            NotInCall();
            TurnOff();

            core.OpenDiscordFromHome();

            Assert.False(Guide.IsOverlayShown);
            Assert.True(core.Settings.IsShown);
            Assert.Equal(ThemeSettingsPage.Discord, core.Settings.ThemePage);
        }

        [Fact]
        public void A_Toast_has_the_Guide_buttons_prompt_while_on_but_not_off()
        {
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });
            Assert.True(core.Toast.HasPrompt);
            clock.Advance(TimeSpan.FromSeconds(10));

            TurnOff();
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated"), new Notification("b", "Update available") });

            Assert.True(core.Toast.IsShown);
            Assert.Equal("Update available", core.Toast.Text);
            Assert.False(core.Toast.HasPrompt);
        }

        [Fact]
        public void Off_a_Toast_s_Guide_button_shortcut_does_nothing()
        {
            TurnOff();
            core.OnNotificationsChanged(new Notification[0]);
            core.OnNotificationsChanged(new[] { new Notification("a", "Library updated") });

            OpenGuide();

            Assert.False(Guide.IsOverlayShown);
            Assert.True(core.Toast.IsShown);
        }

        [Fact]
        public void Off_the_Idle_screen_leaves_out_the_tips_about_the_Guide()
        {
            TurnOff();

            var tips = IdleTipsShown();

            Assert.Equal(8, tips.Count);
            Assert.DoesNotContain(tips, t => t.Contains("Guide") || t.Contains("Discord tab"));
        }

        [Fact]
        public void On_the_Idle_screen_shows_all_twelve_tips()
        {
            Assert.Equal(12, IdleTipsShown().Count);
        }

        [Fact]
        public void Off_a_prompt_shows_on_its_own_without_the_Guide_and_Cancel_hands_back()
        {
            var game = StartHaloWithHadesBeside();
            TurnOff();

            core.PlayGame(game);

            Assert.Equal("Quit Halo Infinite and start Hades?", Guide.Dialog.Title);
            Assert.True(Guide.IsOverlayShown);
            Assert.False(Guide.ShowsPanel);

            Tap(ControllerButton.B);

            Assert.Null(Guide.Dialog);
            Assert.False(Guide.IsOpen);
        }

        [Fact]
        public void Off_a_prompt_about_a_game_that_exits_on_its_own_goes_with_its_dim()
        {
            var hades = StartHaloWithHadesBeside();
            TurnOff();
            core.PlayGame(hades);

            core.OnGameStopped(haloId);

            Assert.Null(Guide.Dialog);
            Assert.False(Guide.IsOpen);
        }

        [Fact]
        public void On_a_prompt_still_shows_over_the_Guide_which_stays_open_on_Cancel()
        {
            var game = StartHaloWithHadesBeside();

            core.PlayGame(game);

            Assert.True(Guide.ShowsPanel);

            Tap(ControllerButton.B);

            Assert.Null(Guide.Dialog);
            Assert.True(Guide.IsOpen);
        }

        /// <summary>Halo Infinite running, and Hades, whose id is given back, to start over it.</summary>
        private Guid StartHaloWithHadesBeside()
        {
            var library = new FakeLibrary()
                .WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12))
                .WithGame("Hades", lastActivity: new DateTime(2026, 10, 11));
            core.OnLibraryChanged(library.Snapshot());
            haloId = library.Id("Halo Infinite");
            core.OnGameStarted(haloId);
            return library.Id("Hades");
        }

        /// <summary>The tips of a whole round of the Idle screen's cards, with no games or achievements: tips only, each once.</summary>
        private List<string> IdleTipsShown()
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            var seen = new List<string>();
            for (var i = 0; i < 24; i++)
            {
                var card = core.Idle.Card;
                if (card != null && card.Kind == IdleCardKind.Tip && !seen.Contains(card.Title))
                {
                    seen.Add(card.Title);
                }

                clock.Advance(TimeSpan.FromSeconds(20));
            }

            return seen;
        }
    }
}
