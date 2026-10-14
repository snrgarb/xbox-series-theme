using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the running Playnite instance.</summary>
    internal sealed class PlayniteHost : IPlayniteHost
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteAPI api;

        /// <summary>The user's own filter and sort while My games &amp; apps has its own on the main list; there is none otherwise.</summary>
        private readonly string userFilterFile;

        public PlayniteHost(IPlayniteAPI api, string dataDirectory)
        {
            this.api = api;
            userFilterFile = Path.Combine(dataDirectory, "UserFilter.json");
        }

        public void BringToFront()
        {
            // Playnite's own main window (Fullscreen). Not GetCurrentAppWindow(): that can be the Overlay, which is active.
            var main = Application.Current?.MainWindow;
            if (main == null)
            {
                logger.Warn("Can't bring Playnite to the front: no main window");
                return;
            }

            var handle = new WindowInteropHelper(main).Handle;
            if (NativeMethods.IsIconic(handle))
            {
                NativeMethods.ShowWindow(handle, NativeMethods.SW_RESTORE);
            }

            NativeMethods.BringToForeground(handle);
        }

        public void BringToFront(DashboardPage page)
        {
            BringToFront();

            // Both pages are Playnite's list view, which the core shows as one or the other; a Game hub open on top of it
            // is its details view.
            if (api.MainView.ActiveFullscreenView == FullscreenView.Details)
            {
                api.MainView.ToggleFullscreenView();
            }
        }

        public void StartGame(Guid gameId)
        {
            api.StartGame(gameId);
        }

        public void OpenGameHub(Guid gameId)
        {
            // The hub is Playnite's details view of the main list's selected game, which opens with focus on Play.
            if (SelectInMainList(gameId) && api.MainView.ActiveFullscreenView == FullscreenView.List)
            {
                api.MainView.ToggleFullscreenView();
            }
        }

        public void OpenGameMenu(Guid gameId)
        {
            if (!SelectInMainList(gameId))
            {
                return;
            }

            // Not in the SDK: Fullscreen's main view model opens its game menu for the main list's selected game.
            var model = Application.Current?.MainWindow?.DataContext;
            if (model?.GetType().GetProperty("OpenGameMenuCommand")?.GetValue(model) is ICommand command)
            {
                command.Execute(null);
            }
            else
            {
                logger.Warn("Can't open Playnite's game menu: this Playnite build has no OpenGameMenuCommand");
            }
        }

        public void SetFavourite(Guid gameId, bool favourite)
        {
            var game = api.Database.Games.Get(gameId);
            if (game == null || game.Favorite == favourite)
            {
                return;
            }

            game.Favorite = favourite;
            api.Database.Games.Update(game);
        }

        public void SaveUserFilter()
        {
            // A copy left by the last session was given back at start (RestoreLeftUserFilter), so what shows now is the user's.
            var view = api.MainView;
            var saved = new UserFilter
            {
                Preset = view.GetActiveFilterPreset(),
                Settings = view.GetCurrentFilterSettings(),
                Sort = view.SortOrder,
                Direction = view.SortOrderDirection,
            };
            Directory.CreateDirectory(Path.GetDirectoryName(userFilterFile));
            File.WriteAllText(userFilterFile, Serialization.ToJson(saved));
        }

        public void RestoreUserFilter()
        {
            if (!File.Exists(userFilterFile))
            {
                logger.Warn("Can't give the user's filter back: it was never saved");
                return;
            }

            var saved = Serialization.FromJsonFile<UserFilter>(userFilterFile);

            // As the user's preset, so Playnite keeps it selected, with the settings and sort they had, edits and all.
            api.MainView.ApplyFilterPreset(new Playnite.SDK.Models.FilterPreset
            {
                Id = saved.Preset,
                Name = api.Database.FilterPresets.Get(saved.Preset)?.Name,
                Settings = saved.Settings,
                SortingOrder = saved.Sort,
                SortingOrderDirection = saved.Direction,
            });

            // Playnite saves its settings before extensions hear that it's closing. Until the user's filter is saved
            // again the copy stays, and the next Fullscreen start gives it back (RestoreLeftUserFilter).
            if (SavePlayniteSettings())
            {
                File.Delete(userFilterFile);
            }
        }

        /// <summary>
        /// Gives back a user's filter that a session left behind, closing or crashing on My games &amp; apps before its
        /// own could be saved. Only in Fullscreen: the filter is Fullscreen's.
        /// </summary>
        public void RestoreLeftUserFilter()
        {
            if (File.Exists(userFilterFile))
            {
                logger.Info("Giving back the user's filter, left by the last session");
                RestoreUserFilter();
            }
        }

        /// <summary>Not in the SDK: Playnite's own settings save. False, and logged, on a build where it can't be found.</summary>
        private static bool SavePlayniteSettings()
        {
            try
            {
                var app = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("Playnite.PlayniteApplication", throwOnError: false))
                    .FirstOrDefault(t => t != null);
                var current = app?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var settings = current?.GetType().GetProperty("AppSettings")?.GetValue(current);
                var save = settings?.GetType().GetMethod("SaveSettings", Type.EmptyTypes);
                if (save == null)
                {
                    logger.Warn("Can't save Playnite's settings: this Playnite build has no AppSettings.SaveSettings");
                    return false;
                }

                save.Invoke(settings, null);
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't save Playnite's settings");
                return false;
            }
        }

        public void ShowInMainList(GameListFilter filter)
        {
            // Not a saved preset: it only sets the main list's filter and sort, which RestoreUserFilter puts back.
            api.MainView.ApplyFilterPreset(new Playnite.SDK.Models.FilterPreset
            {
                Settings = new FilterPresetSettings { IsInstalled = filter.InstalledOnly },
                SortingOrder = ToSortOrder(filter.Sort),
                SortingOrderDirection = filter.Direction == SortDirection.Descending
                    ? SortOrderDirection.Descending
                    : SortOrderDirection.Ascending,
            });
        }

        private static SortOrder ToSortOrder(GameSort sort)
        {
            switch (sort)
            {
                case GameSort.Name: return SortOrder.Name;
                case GameSort.LastActivity: return SortOrder.LastActivity;
                case GameSort.Added: return SortOrder.Added;
                case GameSort.Modified: return SortOrder.Modified;
                case GameSort.ReleaseDate: return SortOrder.ReleaseDate;
                case GameSort.Playtime: return SortOrder.Playtime;
                case GameSort.PlayCount: return SortOrder.PlayCount;
                default: throw new ArgumentOutOfRangeException(nameof(sort), sort, null);
            }
        }

        /// <summary>
        /// Selects the game in Playnite's main list, which the Game hub and the game menu both show. A game outside the
        /// user's current filter can't be selected; showing either then would show another game, so this returns false.
        /// </summary>
        private bool SelectInMainList(Guid gameId)
        {
            api.MainView.SelectGame(gameId);
            if (api.MainView.SelectedGames.Any(g => g.Id == gameId))
            {
                return true;
            }

            logger.Warn($"Can't show {gameId}: it isn't in the main list's current filter");
            return false;
        }

        /// <summary>The user's own Fullscreen filter and sort, as saved in <see cref="userFilterFile"/>.</summary>
        private sealed class UserFilter
        {
            public Guid Preset { get; set; }
            public FilterPresetSettings Settings { get; set; }
            public SortOrder Sort { get; set; }
            public SortOrderDirection Direction { get; set; }
        }
    }
}
