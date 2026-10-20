using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
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

        /// <summary>Playnite's own library update and metadata download, with its progress bar.</summary>
        private readonly PlayniteLibraryTasks libraryTasks;

        /// <summary>
        /// The user's own filter and sort while My games &amp; apps keeps them, so a session that closes or crashes before
        /// they are back in Playnite's saved settings gives them back at the next start; there is none otherwise.
        /// </summary>
        private readonly string userFilterFile;

        /// <summary>The user's own filter and sort while My games &amp; apps is up and has kept them; null otherwise.</summary>
        private UserFilter kept;

        /// <summary>What <see cref="userFilterFile"/> holds, as this session last wrote or read it; null with no file.</summary>
        private string keptOnDisk;

        /// <summary>Whether My games &amp; apps' own filter is on the main list, in place of <see cref="kept"/>.</summary>
        private bool dashboardFilterShown;

        /// <summary>
        /// Bumped by every change to the library's games, on whatever thread Playnite raises it, so a count made before
        /// it isn't used after it.
        /// </summary>
        private int libraryVersion;

        private bool followingLibrary;

        /// <summary>The filter last counted, as JSON, the library as it was then, and how many games it showed.</summary>
        private string countedSettings;

        private int countedVersion;

        private int countedGames;

        public PlayniteHost(IPlayniteAPI api, string dataDirectory)
        {
            this.api = api;
            libraryTasks = new PlayniteLibraryTasks(api);
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

            // Restored first if it's minimised.
            NativeMethods.BringToForeground(new WindowInteropHelper(main).Handle);
        }

        public void BringToFront(DashboardPage page)
        {
            BringToFront();

            // Every page is Playnite's list view, which the core shows as one or another; a Game hub open on top of it is
            // its details view.
            LeaveDetailsView();
        }

        public void CloseGameHub(Guid gameId)
        {
            // The details view shows the main list's selected game: another game's hub is left open.
            if (api.MainView.SelectedGames?.Any(g => g.Id == gameId) == true)
            {
                LeaveDetailsView();
            }
        }

        private void LeaveDetailsView()
        {
            if (api.MainView.ActiveFullscreenView == FullscreenView.Details)
            {
                api.MainView.ToggleFullscreenView();
            }
        }

        public void StartGame(Guid gameId)
        {
            api.StartGame(gameId);
        }

        public void InstallGame(Guid gameId) => api.InstallGame(gameId);

        public void UninstallGame(Guid gameId) => api.UninstallGame(gameId);

        public void StartApp(App app)
        {
            // Not in the SDK: Playnite's own start of a Software tool, as its Software tools menu runs it. It runs script
            // tools too, and shows Playnite's error when the App can't start. Later: that error is a modal window.
            var model = Application.Current?.MainWindow?.DataContext;
            var start = model?.GetType().GetMethod("StartSoftwareTool", new[] { typeof(AppSoftware) });
            AppSoftware tool = null;
            try
            {
                tool = start == null ? null : PlayniteLibrary.SoftwareTools()?.FirstOrDefault(t => t.Id == app.Id);
            }
            catch (Exception e)
            {
                logger.Warn(e, "Couldn't read Playnite's Software tools");
            }

            if (tool != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        start.Invoke(model, new object[] { tool });
                    }
                    catch (Exception e)
                    {
                        ShowStartupError(app, (e as TargetInvocationException)?.InnerException ?? e);
                    }
                }));
                return;
            }

            logger.Warn($"Can't start {app.Name} as Playnite does: this Playnite build has no StartSoftwareTool, or no such tool");
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => StartProcess(app)));
        }

        /// <summary>
        /// Starts an App's path with its arguments as Playnite does when it can't run its own start: in its working
        /// directory, or else the one its path is in. An App that can't start, or a script tool, which has no path to
        /// start, shows Playnite's error for it.
        /// </summary>
        private void StartProcess(App app)
        {
            try
            {
                if (string.IsNullOrEmpty(app.Path))
                {
                    throw new InvalidOperationException($"{app.Name} has no path to start; only Playnite can run a script tool.");
                }

                var path = app.Path.Contains("..") ? Path.GetFullPath(app.Path) : app.Path;
                Process.Start(new ProcessStartInfo(path)
                {
                    Arguments = app.Arguments ?? string.Empty,
                    WorkingDirectory = string.IsNullOrEmpty(app.WorkingDirectory)
                        ? new FileInfo(path).Directory.FullName
                        : app.WorkingDirectory,
                });
            }
            catch (Exception e)
            {
                ShowStartupError(app, e);
            }
        }

        /// <summary>Logs an App that couldn't start and shows Playnite's own startup error for it.</summary>
        private void ShowStartupError(App app, Exception e)
        {
            logger.Error(e, $"Couldn't start {app.Name}");
            api.Dialogs.ShowErrorMessage(
                ResourceProvider.GetString("LOCAppStartupError") + "\n\n" + e.Message,
                ResourceProvider.GetString("LOCStartupError"));
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

            // Fullscreen's game menu is for the main list's selected game.
            FullscreenCommand("OpenGameMenuCommand")?.Execute(null);
        }

        public void RunGameAction(Guid gameId, int index)
        {
            var game = api.Database.Games.Get(gameId);
            var action = game?.GameActions?.Where(a => !a.IsPlayAction).ElementAtOrDefault(index);
            if (action == null)
            {
                return;
            }

            // Not in the SDK: what Playnite's game menu runs for the row, GamesEditor.ActivateAction, which starts a file,
            // URL, script or emulator action as Playnite does and reports its own errors.
            var editor = PlayniteApplication()?.GetType().GetProperty("GamesEditor")?.GetValue(PlayniteApplication());
            var activate = editor?.GetType().GetMethod("ActivateAction", new[] { typeof(Game), typeof(GameAction) });
            if (activate == null)
            {
                logger.Warn("Can't run a game's action: this Playnite build has no GamesEditor.ActivateAction");
                return;
            }

            Later(() =>
            {
                try
                {
                    activate.Invoke(editor, new object[] { game, action });
                }
                catch (Exception e)
                {
                    logger.Error(e, $"Couldn't run {game.Name}'s action {action.Name}");
                }
            });
        }

        public void SetHidden(Guid gameId, bool hidden)
        {
            var game = api.Database.Games.Get(gameId);
            if (game != null && game.Hidden != hidden)
            {
                game.Hidden = hidden;
                api.Database.Games.Update(game);
            }
        }

        /// <summary>Not in the SDK: Playnite's own check, <c>Playnite.HdrUtilities.IsHdrSupported</c>, the primary display's. False on a build without it.</summary>
        public bool IsHdrSupported
        {
            get
            {
                try
                {
                    var check = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("Playnite.HdrUtilities", throwOnError: false))
                        .FirstOrDefault(t => t != null)
                        ?.GetMethod("IsHdrSupported", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    return check?.Invoke(null, null) is bool supported && supported;
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't ask Playnite whether the display supports HDR");
                    return false;
                }
            }
        }

        public void SetSystemHdr(Guid gameId, bool enabled)
        {
            var game = api.Database.Games.Get(gameId);
            if (game != null && game.EnableSystemHdr != enabled)
            {
                game.EnableSystemHdr = enabled;
                api.Database.Games.Update(game);
            }
        }

        public void OpenExtensionsMenu(Guid gameId)
        {
            // Not in the SDK: what the game menu's Extensions runs, ExtensionsMenuViewModels over the main view model,
            // listing every extension's game menu items for the game.
            var game = api.Database.Games.Get(gameId);
            var model = Application.Current?.MainWindow?.DataContext;
            var app = model?.GetType().Assembly;
            var factory = app?.GetType("Playnite.FullscreenApp.Windows.ExtensionsMenuWindowFactory");
            var menu = app?.GetType("Playnite.FullscreenApp.ViewModels.ExtensionsMenuViewModels");
            var open = menu?.GetMethod("OpenView", Type.EmptyTypes);
            if (game == null || factory == null || open == null)
            {
                logger.Warn("Can't open Playnite's Extensions picker: this Playnite build has no ExtensionsMenuViewModels");
                return;
            }

            Later(() =>
            {
                try
                {
                    open.Invoke(Activator.CreateInstance(menu, Activator.CreateInstance(factory), model, game), null);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't open Playnite's Extensions picker");
                }
            });
        }

        public void RemoveGame(Guid gameId)
        {
            // The theme has asked already; Playnite's own RemoveGame would ask again.
            if (api.Database.Games.Get(gameId) != null)
            {
                api.Database.Games.Remove(gameId);
            }
        }

        public void OpenPlayniteSettings()
        {
            // Not a command of the main view model, but of the main menu's: its Settings builds the window's view model
            // over the main one and opens it, as here.
            var model = Application.Current?.MainWindow?.DataContext;
            var app = model?.GetType().Assembly;
            var factory = app?.GetType("Playnite.FullscreenApp.Windows.SettingsWindowFactory");
            var settings = app?.GetType("Playnite.FullscreenApp.ViewModels.SettingsViewModel");
            var open = settings?.GetMethod("OpenView", Type.EmptyTypes);
            if (factory == null || open == null)
            {
                logger.Warn("Can't open Playnite's Fullscreen settings: this Playnite build has no SettingsViewModel");
                return;
            }

            Later(() =>
            {
                try
                {
                    open.Invoke(Activator.CreateInstance(settings, Activator.CreateInstance(factory), model), null);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't open Playnite's Fullscreen settings");
                }
            });
        }

        public bool IsLibraryBusy => libraryTasks.IsBusy;

        public void UpdateLibrary(Action<IReadOnlyList<Guid>> done) => libraryTasks.UpdateLibrary(done);

        public void DownloadMetadata(IReadOnlyList<Guid> games, Action<bool> done) => libraryTasks.DownloadMetadata(games, done);

        public void DownloadMissingMetadata(Action<bool> done) => libraryTasks.DownloadMissingMetadata(done);

        public void CancelLibraryTask() => libraryTasks.CancelRun();

        public void OpenMainMenu()
        {
            // What the top bar's PART_ButtonMainMenu runs, theme-only.
            Later(FullscreenCommand("OpenMainMenuCommand"));
        }
#if DEBUG

        public void AddNotification(string id, string text)
        {
            api.Notifications.Add(new NotificationMessage(id, text, NotificationType.Info));
        }
#endif

        public void Power(PowerCommand command)
        {
            // Carried out here, as Playnite's own Computer class does, not through its main menu's commands: those ask
            // "Are you sure?" again, and our dialog already asked. Shutting down or restarting, Playnite saves and closes
            // on its own as Windows ends the session.
            switch (command)
            {
                case PowerCommand.TurnOffPc:
                    Later(() => RunShutdown("/s /hybrid /t 0"));
                    break;
                case PowerCommand.RestartPc:
                    Later(() => RunShutdown("-r -t 0"));
                    break;
                case PowerCommand.Sleep:
                    Later(Sleep);
                    break;
                case PowerCommand.ExitPlaynite:
                    Later(ExitPlaynite);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command), command, null);
            }
        }

        private static void RunShutdown(string arguments)
        {
            try
            {
                using (Process.Start(new ProcessStartInfo("shutdown.exe", arguments) { CreateNoWindow = true, UseShellExecute = false }))
                {
                }
            }
            catch (Exception e)
            {
                logger.Error(e, $"Couldn't run shutdown.exe {arguments}");
            }
        }

        private static void Sleep()
        {
            if (!NativeMethods.SetSuspendState(hibernate: false, forceCritical: true, disableWakeEvent: false))
            {
                logger.Error($"Couldn't put the PC to sleep: error {Marshal.GetLastWin32Error()}");
            }
        }

        /// <summary>
        /// Not in the SDK: Playnite's own exit, <c>PlayniteApplication.Current.Quit()</c>, which saves settings and releases
        /// resources first. On a Playnite build without it, the WPF shutdown, after which Playnite still releases them.
        /// </summary>
        private static void ExitPlaynite()
        {
            try
            {
                var app = PlayniteApplication();
                var quit = app?.GetType().GetMethod("Quit", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (quit != null)
                {
                    quit.Invoke(app, new object[] { true });
                    return;
                }

                logger.Warn("Can't quit as Playnite does: this Playnite build has no PlayniteApplication.Current.Quit");
            }
            catch (Exception e)
            {
                logger.Error(e, "Couldn't quit as Playnite does");
            }

            Application.Current?.Shutdown();
        }

        /// <summary>Not in the SDK: Playnite's <c>PlayniteApplication.Current</c>, which the WPF application only wraps. Null on a build without it.</summary>
        private static object PlayniteApplication() =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Playnite.PlayniteApplication", throwOnError: false))
                .FirstOrDefault(t => t != null)
                ?.GetProperty("Current", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null);

        /// <summary>
        /// Not in the SDK: one of the commands Fullscreen's own menus run, from its main view model. Null, and logged,
        /// on a Playnite build without it.
        /// </summary>
        internal static ICommand FullscreenCommand(string name)
        {
            var model = Application.Current?.MainWindow?.DataContext;
            if (model?.GetType().GetProperty(name)?.GetValue(model) is ICommand command)
            {
                return command;
            }

            logger.Warn($"Can't run Playnite's {name}: this Playnite build has no such command");
            return null;
        }

        /// <summary>
        /// Runs <paramref name="command"/> once the caller is done: settings and the main menu are modal windows, and the
        /// Guide has to finish handing focus back before they open.
        /// </summary>
        private static void Later(ICommand command)
        {
            if (command != null)
            {
                Later(() => command.Execute(null));
            }
        }

        /// <summary>Runs <paramref name="action"/> once the caller is done, as <see cref="Later(ICommand)"/>.</summary>
        private static void Later(Action action)
        {
            Application.Current.Dispatcher.BeginInvoke(action);
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
            // A copy left by the last session was given back at start (RestoreLeftUserFilter), so what shows now is the
            // user's, as Full library left it if they were there.
            var view = api.MainView;
            kept = new UserFilter
            {
                Preset = view.GetActiveFilterPreset(),
                Settings = view.GetCurrentFilterSettings(),
                Sort = view.SortOrder,
                Direction = view.SortOrderDirection,
            };
            dashboardFilterShown = true;

            // Written the first time, and again only when Full library has changed it since: each move along the nav from
            // Full library to Games or Play history otherwise leaves the file as it is.
            var json = Serialization.ToJson(kept);
            if (json != keptOnDisk)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(userFilterFile));
                File.WriteAllText(userFilterFile, json);
                keptOnDisk = json;
            }
        }

        public void RestoreUserFilter()
        {
            if (kept == null)
            {
                logger.Warn("Can't give the user's filter back: it was never saved");
                return;
            }

            // Only onto the main list, as each move onto Full library asks: the file stays, and Playnite's settings
            // aren't saved, until the page is done with the main list (ReleaseUserFilter). Should Playnite crash
            // meanwhile, the file gives the next start the same filter back.
            Apply(kept);
            dashboardFilterShown = false;
        }

        public void ReleaseUserFilter()
        {
            if (kept == null)
            {
                return;
            }

            if (dashboardFilterShown)
            {
                RestoreUserFilter();
            }

            kept = null;
            ForgetFileOnceSaved();
        }

        /// <summary>
        /// Gives back a user's filter that a session left behind, closing or crashing on My games &amp; apps before its
        /// own could be saved. Only in Fullscreen: the filter is Fullscreen's.
        /// </summary>
        public void RestoreLeftUserFilter()
        {
            if (!File.Exists(userFilterFile))
            {
                return;
            }

            logger.Info("Giving back the user's filter, left by the last session");
            keptOnDisk = File.ReadAllText(userFilterFile);
            Apply(Serialization.FromJson<UserFilter>(keptOnDisk));
            ForgetFileOnceSaved();
        }

        /// <summary>Puts <paramref name="filter"/>, the user's, on the main list.</summary>
        private void Apply(UserFilter filter)
        {
            // As the user's preset, so Playnite keeps it selected, with the settings and sort they had, edits and all.
            api.MainView.ApplyFilterPreset(new Playnite.SDK.Models.FilterPreset
            {
                Id = filter.Preset,
                Name = api.Database.FilterPresets.Get(filter.Preset)?.Name,
                Settings = filter.Settings,
                SortingOrder = filter.Sort,
                SortingOrderDirection = filter.Direction,
            });
        }

        /// <summary>
        /// With the user's filter back on the main list, saves Playnite's settings, and deletes the file once they hold
        /// it. Playnite saves its settings before extensions hear that it's closing: until they are saved again the copy
        /// stays, and the next Fullscreen start gives it back (RestoreLeftUserFilter).
        /// </summary>
        private void ForgetFileOnceSaved()
        {
            if (keptOnDisk == null || !SavePlayniteSettings())
            {
                return;
            }

            File.Delete(userFilterFile);
            keptOnDisk = null;
        }

        /// <summary>Not in the SDK: Playnite's own settings save. False, and logged, on a build where it can't be found.</summary>
        private static bool SavePlayniteSettings()
        {
            try
            {
                var current = PlayniteApplication();
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

        public void SelectFilterPreset(Guid presetId)
        {
            // The saved preset itself, as its tab applies it, so Playnite keeps it selected and lit.
            api.MainView.ApplyFilterPreset(presetId);
        }

        public GameSort? MainListSort => PlayniteLibrary.ToGameSort(api.MainView.SortOrder);

        public SortDirection MainListSortDirection =>
            api.MainView.SortOrderDirection == SortOrderDirection.Descending ? SortDirection.Descending : SortDirection.Ascending;

        public void SortMainList(GameSort sort, SortDirection direction)
        {
            // The SDK only reads the sort; Fullscreen's own main view sets it too, as its sort menu does, leaving the
            // filter and the active preset (its tab) as they are.
            var view = api.MainView;
            var order = view.GetType().GetProperty(nameof(IMainViewAPI.SortOrder));
            var way = view.GetType().GetProperty(nameof(IMainViewAPI.SortOrderDirection));
            if (order?.CanWrite != true || way?.CanWrite != true)
            {
                logger.Warn("Can't sort the main list: this Playnite build's main view can't set its sort");
                return;
            }

            order.SetValue(view, ToSortOrder(sort));
            way.SetValue(view, direction == SortDirection.Descending ? SortOrderDirection.Descending : SortOrderDirection.Ascending);
        }

        public void FilterMainListByName(string name)
        {
            // Not in the SDK: Fullscreen's own name filter, as Y's text entry sets it, on top of the active preset, which
            // stays lit as its tab. A preset applied for the name would replace the user's edits of it.
            var settings = FullscreenFilterSettings();
            var nameProperty = settings?.GetType().GetProperty("Name");
            if (nameProperty?.CanWrite != true)
            {
                logger.Warn("Can't filter the main list by name: this Playnite build has no Fullscreen FilterSettings.Name");
                return;
            }

            nameProperty.SetValue(settings, string.IsNullOrEmpty(name) ? null : name);
        }

        /// <summary>Not in the SDK: Fullscreen's live filter settings, AppSettings.Fullscreen.FilterSettings; null if not found.</summary>
        private static object FullscreenFilterSettings()
        {
            object Get(object owner, string name) => owner?.GetType().GetProperty(name)?.GetValue(owner);
            var model = Application.Current?.MainWindow?.DataContext;
            return Get(Get(Get(model, "AppSettings"), "Fullscreen"), "FilterSettings");
        }

        public int? CountUserFilterGames()
        {
            try
            {
                FollowLibrary();

                // As Playnite's own lists evaluate a filter, as the preset Groups are. That runs over the whole library,
                // so not again for the same filter over the same games: each move onto Full library asks, as does every
                // library push.
                var settings = dashboardFilterShown ? kept.Settings : api.MainView.GetCurrentFilterSettings();
                var key = Serialization.ToJson(settings);
                var version = Volatile.Read(ref libraryVersion);
                if (key != countedSettings || version != countedVersion)
                {
                    countedGames = api.Database.GetFilteredGames(settings).Count();
                    countedSettings = key;
                    countedVersion = version;
                }

                return countedGames;
            }
            catch (Exception e)
            {
                // Home's elements can be built before Playnite's main view is fully up.
                logger.Warn(e, "Couldn't count the games under the user's filter");
                return null;
            }
        }

        /// <summary>
        /// Keeps <see cref="libraryVersion"/> up with the library's games, from the first count, when the database is
        /// open. Bumped as Playnite raises the change, before the library push the extension queues for it on the UI
        /// thread, so a count that push asks for is made afresh.
        /// </summary>
        private void FollowLibrary()
        {
            if (followingLibrary)
            {
                return;
            }

            api.Database.Games.ItemCollectionChanged += (_, __) => Interlocked.Increment(ref libraryVersion);
            api.Database.Games.ItemUpdated += (_, __) => Interlocked.Increment(ref libraryVersion);
            followingLibrary = true;
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
