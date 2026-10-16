using System;
using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core.Tests.Fakes;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>The Audio flyout's Output and Input dropdowns: their lists of Windows' devices, choosing one, and following Windows.</summary>
    public class AudioDeviceTests
    {
        private readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 14, 20, 0, 0));
        private readonly FakeWindowSystem windows = new FakeWindowSystem();
        private readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        private readonly FakeAudio audio = new FakeAudio { Volume = 50 };
        private readonly ExtensionCore core;

        public AudioDeviceTests()
        {
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), new FakeUserAccount(), audio, new FakeController(), new FakeDiscordTransport(), new FakeDiscordOAuth(), new FakeSecretStore(), new FakeDownloads());
            core.OnLibraryChanged(new FakeLibrary().WithGame("Halo Infinite", lastActivity: new DateTime(2026, 10, 12)).Snapshot());
            audio.Add("speakers", "Speakers", AudioDeviceKind.Output);
            audio.Add("headphones", "Headphones", AudioDeviceKind.Output);
            audio.Add("tv", "LG TV", AudioDeviceKind.Output);
            audio.Add("mic", "Microphone", AudioDeviceKind.Input);
            audio.Add("headset", "Headset Microphone", AudioDeviceKind.Input);
        }

        [Fact]
        public void Audio_has_Output_and_Input_above_Volume_and_Mute_naming_Windows_defaults_with_focus_on_Output()
        {
            audio.SetDefault(AudioDeviceKind.Output, "headphones");

            OpenAudio();

            Assert.Equal(new[] { GuideItemKind.AudioOutput, GuideItemKind.AudioInput, GuideItemKind.Volume, GuideItemKind.Mute }, Flyout.Select(i => i.Kind));
            Assert.Equal(new[] { "Output", "Input" }, Flyout.Take(2).Select(i => i.Title));
            Assert.Equal("Headphones", Output.Selection);
            Assert.Equal("Microphone", Input.Selection);
            Assert.False(Output.IsExpanded);
            Assert.Null(Output.Options);
            Assert.Same(Output, core.Guide.FocusedItem);
        }

        [Fact]
        public void A_on_Output_opens_its_list_of_devices_the_default_marked_and_focused()
        {
            audio.SetDefault(AudioDeviceKind.Output, "headphones");
            OpenAudio();
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.True(Output.IsExpanded);
            Assert.Equal(new[] { "Speakers", "Headphones", "LG TV" }, Output.Options.Select(o => o.Title));
            Assert.All(Output.Options, o => Assert.Equal(GuideItemKind.AudioDevice, o.Kind));
            Assert.Equal(new[] { false, true, false }, Output.Options.Select(o => o.IsOn));
            Assert.Same(Output.Options[1], core.Guide.FocusedItem);
            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
        }

        [Fact]
        public void A_on_Input_opens_its_list_of_input_devices()
        {
            OpenAudio();
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.A);

            Assert.Equal(new[] { "Microphone", "Headset Microphone" }, Input.Options.Select(o => o.Title));
            Assert.Same(Input.Options[0], core.Guide.FocusedItem);
            Assert.False(Output.IsExpanded);
        }

        [Fact]
        public void With_no_default_the_dropdown_says_None_and_its_list_opens_on_the_first_device()
        {
            audio.SetDefault(AudioDeviceKind.Output, null);
            OpenAudio();
            Assert.Equal("None", Output.Selection);

            Tap(ControllerButton.A);

            Assert.All(Output.Options, o => Assert.False(o.IsOn));
            Assert.Same(Output.Options[0], core.Guide.FocusedItem);
        }

        [Theory]
        [InlineData(AudioDeviceKind.Input)]
        [InlineData(AudioDeviceKind.Output)]
        public void With_no_device_of_its_kind_a_dropdown_says_None_and_A_does_nothing_on_it(AudioDeviceKind kind)
        {
            foreach (var device in audio.Devices(kind).ToList())
            {
                audio.Remove(device.Id);
            }

            OpenAudio();
            var dropdown = kind == AudioDeviceKind.Output ? Output : Input;
            if (kind == AudioDeviceKind.Input)
            {
                Tap(ControllerButton.DPadDown);
            }

            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal("None", dropdown.Selection);
            Assert.False(dropdown.IsExpanded);
            Assert.Same(dropdown, core.Guide.FocusedItem);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void Up_and_down_move_through_an_open_list_held_at_both_ends()
        {
            OpenAudio();
            Tap(ControllerButton.A);
            sounds.Played.Clear();

            Tap(ControllerButton.DPadUp);
            Assert.Same(Output.Options[0], core.Guide.FocusedItem);
            Assert.Empty(sounds.Played);

            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            Assert.Same(Output.Options[2], core.Guide.FocusedItem);
            Assert.Equal(new[] { UiSound.Navigate, UiSound.Navigate }, sounds.Played);
            Assert.True(Output.IsExpanded);
        }

        [Fact]
        public void Left_and_right_Menu_and_LB_and_RB_leave_an_open_list_alone()
        {
            audio.Volume = 50;
            OpenAudio();
            Tap(ControllerButton.A);
            var focused = core.Guide.FocusedItem;
            sounds.Played.Clear();

            Tap(ControllerButton.DPadLeft);
            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.Start);
            Tap(ControllerButton.LeftShoulder);
            Tap(ControllerButton.RightShoulder);

            Assert.Same(focused, core.Guide.FocusedItem);
            Assert.True(Output.IsExpanded);
            Assert.Null(core.Guide.Dialog);
            Assert.Equal(GuideTabKind.Landing, core.Guide.Tab);
            Assert.Equal(50, audio.Volume);
            Assert.Empty(sounds.Played);
        }

        [Fact]
        public void A_on_a_device_makes_it_Windows_default_closing_the_list_onto_the_dropdown_which_names_it()
        {
            OpenAudio();
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.DPadDown);
            sounds.Played.Clear();

            Tap(ControllerButton.A);

            Assert.Equal(new[] { "tv" }, audio.DefaultsSet.Select(d => d.Id));
            Assert.Equal(AudioDeviceKind.Output, audio.DefaultsSet[0].Kind);
            Assert.False(Output.IsExpanded);
            Assert.Same(Output, core.Guide.FocusedItem);
            Assert.Equal("LG TV", Output.Selection);
            Assert.Equal(new[] { UiSound.Enter }, sounds.Played);
            Assert.True(core.Guide.IsOpen);
        }

        [Fact]
        public void The_dropdown_shows_what_Windows_reports_not_what_was_asked()
        {
            OpenAudio();
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);
            Assert.Equal("Headphones", Output.Selection);

            audio.SetDefault(AudioDeviceKind.Output, "speakers");
            core.OnAudioDevicesChanged();

            Assert.Equal("Speakers", Output.Selection);
        }

        [Fact]
        public void B_closes_the_list_unchanged_onto_the_dropdown_and_B_again_closes_the_flyout()
        {
            OpenAudio();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);

            Tap(ControllerButton.B);

            Assert.Empty(audio.DefaultsSet);
            Assert.False(Input.IsExpanded);
            Assert.Equal("Microphone", Input.Selection);
            Assert.Same(Input, core.Guide.FocusedItem);
            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);

            Tap(ControllerButton.B);

            Assert.Null(core.Guide.Flyout);
            Assert.Equal(GuideItemKind.Audio, core.Guide.FocusedItem.Kind);
        }

        [Fact]
        public void A_device_plugged_in_while_the_list_is_open_joins_it_focus_staying_on_the_same_device()
        {
            OpenAudio();
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadDown);
            Assert.Equal("Headphones", core.Guide.FocusedItem.Title);

            audio.Add("usb", "USB Speakers", AudioDeviceKind.Output);
            core.OnAudioDevicesChanged();

            Assert.Equal(new[] { "Speakers", "Headphones", "LG TV", "USB Speakers" }, Output.Options.Select(o => o.Title));
            Assert.Equal("Headphones", core.Guide.FocusedItem.Title);
            Assert.Contains(core.Guide.FocusedItem, Output.Options);
            Assert.True(core.Guide.FocusedItem.IsFocused);
        }

        [Fact]
        public void The_focused_device_unplugged_while_the_list_is_open_moves_focus_to_the_new_default()
        {
            audio.SetDefault(AudioDeviceKind.Output, "tv");
            OpenAudio();
            Tap(ControllerButton.A);
            Tap(ControllerButton.DPadUp);
            Assert.Equal("Headphones", core.Guide.FocusedItem.Title);

            audio.Remove("headphones");
            core.OnAudioDevicesChanged();

            Assert.Equal(new[] { "Speakers", "LG TV" }, Output.Options.Select(o => o.Title));
            Assert.Same(Output.Options[1], core.Guide.FocusedItem);
            Assert.True(Output.Options[1].IsOn);
        }

        [Fact]
        public void The_last_device_of_an_open_list_unplugged_closes_it_onto_the_dropdown_saying_None()
        {
            OpenAudio();
            Tap(ControllerButton.DPadDown);
            Tap(ControllerButton.A);

            audio.Remove("mic");
            audio.Remove("headset");
            core.OnAudioDevicesChanged();

            Assert.False(Input.IsExpanded);
            Assert.Same(Input, core.Guide.FocusedItem);
            Assert.Equal("None", Input.Selection);
        }

        [Fact]
        public void The_default_unplugged_shows_the_one_Windows_picks_next_and_the_volume_and_mute_follow()
        {
            OpenAudio();
            var items = Flyout;

            audio.Remove("speakers");
            audio.Volume = 20;
            audio.IsMuted = true;
            core.OnAudioDevicesChanged();

            Assert.Same(items, Flyout);
            Assert.Equal("Headphones", Output.Selection);
            Assert.Equal(20, Flyout.Single(i => i.Kind == GuideItemKind.Volume).Level);
            Assert.True(Flyout.Single(i => i.Kind == GuideItemKind.Mute).IsOn);
            Assert.Same(Output, core.Guide.FocusedItem);
        }

        [Fact]
        public void Devices_changing_with_the_Guide_closed_change_nothing_and_Audio_opens_on_what_is_there_then()
        {
            core.OnAudioDevicesChanged();
            Assert.False(core.Guide.IsOpen);

            audio.Remove("speakers");
            core.OnAudioDevicesChanged();
            OpenAudio();

            Assert.Equal("Headphones", Output.Selection);
        }

        [Fact]
        public void Closing_the_Guide_with_a_list_open_leaves_none_open_when_Audio_opens_again()
        {
            OpenAudio();
            Tap(ControllerButton.A);

            Tap(ControllerButton.Guide);
            Assert.False(core.Guide.IsOpen);
            OpenAudio();

            Assert.False(Output.IsExpanded);
            Assert.Same(Output, core.Guide.FocusedItem);
        }

        [Fact]
        public void The_mouse_reaches_an_open_list_options_and_clicking_one_makes_it_the_default()
        {
            OpenAudio();
            Tap(ControllerButton.A);

            core.FocusItem(Input);
            core.FocusItem(Output);
            core.FocusItem(core.Guide.Utilities[0]);
            Assert.Same(Output.Options[0], core.Guide.FocusedItem);
            Assert.False(Output.IsFocused);
            Assert.Single(Output.Options, o => o.IsFocused);

            core.FocusItem(Output.Options[2]);
            Assert.Same(Output.Options[2], core.Guide.FocusedItem);

            core.ChooseItem(Output.Options[1]);

            Assert.Equal("headphones", audio.DefaultsSet.Single().Id);
            Assert.False(Output.IsExpanded);
            Assert.Same(Output, core.Guide.FocusedItem);
            Assert.Equal("Headphones", Output.Selection);
        }

        [Fact]
        public void Clicking_a_dropdown_opens_its_list()
        {
            OpenAudio();

            core.ChooseItem(Input);

            Assert.True(Input.IsExpanded);
            Assert.Same(Input.Options[0], core.Guide.FocusedItem);
        }

        private IReadOnlyList<GuideItem> Flyout => core.Guide.Flyout.Items;

        private GuideItem Output => Flyout.Single(i => i.Kind == GuideItemKind.AudioOutput);

        private GuideItem Input => Flyout.Single(i => i.Kind == GuideItemKind.AudioInput);

        private void OpenAudio()
        {
            Tap(ControllerButton.Guide);
            foreach (var _ in core.Guide.Items)
            {
                Tap(ControllerButton.DPadDown);
            }

            Tap(ControllerButton.DPadRight);
            Tap(ControllerButton.A);
            Assert.Equal(GuideItemKind.Audio, core.Guide.Flyout.Kind);
        }

        private void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
            if (button == ControllerButton.Guide)
            {
                clock.Advance(TimeSpan.FromMilliseconds(300));
            }
        }
    }
}
