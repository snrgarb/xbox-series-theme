using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Audio flyout's Output and Input dropdowns: each names Windows' default device of its kind, and A opens a list
    /// of the devices active now in place under it, focus on the default. A on a device makes it the default and B closes
    /// the list unchanged, focus back on the dropdown either way. The flyout follows Windows as devices come and go.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The Audio flyout's dropdown whose list is open, which has the pad; null when none is.</summary>
        private GuideItem OpenDropdown =>
            Guide.Flyout?.Kind == GuideItemKind.Audio ? Guide.Flyout.Items.FirstOrDefault(i => i.IsExpanded) : null;

        private static bool IsDropdown(GuideItem item) => item.Kind == GuideItemKind.AudioOutput || item.Kind == GuideItemKind.AudioInput;

        private static AudioDeviceKind DeviceKind(GuideItem dropdown) =>
            dropdown.Kind == GuideItemKind.AudioInput ? AudioDeviceKind.Input : AudioDeviceKind.Output;

        /// <summary>The Output and Input dropdowns, each naming Windows' default device of its kind.</summary>
        private IEnumerable<GuideItem> AudioDropdowns() => new[]
        {
            new GuideItem(GuideItemKind.AudioOutput, "Output") { Selection = DefaultDeviceName(AudioDeviceKind.Output) },
            new GuideItem(GuideItemKind.AudioInput, "Input") { Selection = DefaultDeviceName(AudioDeviceKind.Input) },
        };

        /// <summary>The name of Windows' default device of <paramref name="kind"/>, or "None" when there is none.</summary>
        private string DefaultDeviceName(AudioDeviceKind kind)
        {
            var id = audio.DefaultDeviceId(kind);
            return audio.Devices(kind).FirstOrDefault(d => d.Id == id)?.Name ?? "None";
        }

        /// <summary>Whether A does nothing on <paramref name="item"/> for want of devices: a dropdown with none of its kind.</summary>
        private bool IsEmptyDropdown(GuideItem item) => IsDropdown(item) && audio.Devices(DeviceKind(item)).Count == 0;

        /// <summary>
        /// Opens <paramref name="dropdown"/>'s list of the devices of its kind active now, or opens it afresh, the default
        /// marked; focus on the device with <paramref name="focusId"/> if it is still there, else on the default, else the
        /// first. With none, the list closes instead.
        /// </summary>
        private void OpenDeviceList(GuideItem dropdown, string focusId)
        {
            var kind = DeviceKind(dropdown);
            var defaultId = audio.DefaultDeviceId(kind);
            var options = audio.Devices(kind)
                .Select(d => new GuideItem(GuideItemKind.AudioDevice, d.Name) { Device = d, IsOn = d.Id == defaultId })
                .ToList();
            if (options.Count == 0)
            {
                CloseDeviceList(dropdown);
                return;
            }

            dropdown.Options = options;
            Guide.FocusedItem = options.FirstOrDefault(o => o.Device.Id == focusId) ?? options.FirstOrDefault(o => o.IsOn) ?? options[0];
        }

        /// <summary>Closes <paramref name="dropdown"/>'s list, focus back on it.</summary>
        private void CloseDeviceList(GuideItem dropdown)
        {
            Guide.FocusedItem = dropdown;
            dropdown.Options = null;
        }

        /// <summary>
        /// A on a device: Windows' default of its kind, the list closing onto its dropdown, which names what Windows then
        /// reports as the default.
        /// </summary>
        private void ChooseDevice(GuideItem option)
        {
            var dropdown = OpenDropdown;
            audio.SetDefaultDevice(option.Device);
            CloseDeviceList(dropdown);
            dropdown.Selection = DefaultDeviceName(option.Device.Kind);
        }

        /// <summary>
        /// Windows' audio devices or its defaults changed: an open Audio flyout follows, its dropdowns and any open list
        /// showing what Windows reports now, and its volume and mute those of the default output. Called on the UI thread.
        /// </summary>
        public void OnAudioDevicesChanged()
        {
            if (Guide.IsOpen && Guide.Flyout?.Kind == GuideItemKind.Audio)
            {
                RefreshAudioFlyout();
            }
        }

        /// <summary>
        /// The open Audio flyout, as Windows and Discord report now: its entries take their new states in place, an open
        /// list following the devices, focus staying where it was; when the entries themselves change, as a call begins
        /// or ends, the flyout is rebuilt, any list closed first so focus lands on an entry still there.
        /// </summary>
        private void RefreshAudioFlyout()
        {
            var items = AudioItems();
            var dropdown = OpenDropdown;
            if (UpdateInPlace(Guide.Flyout.Items, items))
            {
                if (dropdown != null)
                {
                    OpenDeviceList(dropdown, Guide.FocusedItem?.Device?.Id);
                }

                return;
            }

            if (dropdown != null)
            {
                CloseDeviceList(dropdown);
            }

            var focus = FocusAfterRebuild(Guide.Flyout.Items, items);
            Guide.Flyout = new GuideFlyout(GuideItemKind.Audio, "Audio", items);
            Guide.FocusedItem = focus;
        }
    }
}
