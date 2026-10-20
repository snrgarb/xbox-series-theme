using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Not in the SDK: Playnite's own library update and metadata download, run against Fullscreen's main view model
    /// (<c>Playnite.ViewModels.MainViewModelBase</c>) with its progress bar, as Desktop mode runs them. Names checked
    /// against Playnite 10.63. On a build without one of them each call logs, does nothing and still calls back.
    /// Every callback runs once, on the UI thread, also when the run failed or was cancelled.
    /// </summary>
    internal sealed class PlayniteLibraryTasks
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const BindingFlags Statics = BindingFlags.Public | BindingFlags.Static;
        private readonly IPlayniteAPI api;

        public PlayniteLibraryTasks(IPlayniteAPI api)
        {
            this.api = api;
        }

        /// <summary>Fullscreen's main view model, a MainViewModelBase; null before it's up.</summary>
        private static object Model => Application.Current?.MainWindow?.DataContext;

        private static object Get(object owner, string name) => owner?.GetType().GetProperty(name)?.GetValue(owner);

        private static void Set(object owner, string name, object value) => owner?.GetType().GetProperty(name)?.SetValue(owner, value);

        /// <summary>A type of Playnite's own, by full name, from whichever loaded assembly has it; null if none does.</summary>
        private static Type PlayniteType(string name) =>
            AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType(name, throwOnError: false))
                .FirstOrDefault(t => t != null);

        /// <summary>Playnite's static <c>Playnite.GlobalTaskHandler</c>: the progress bar's task and its cancel token.</summary>
        private static Type TaskHandler => PlayniteType("Playnite.GlobalTaskHandler");

        public bool IsBusy
        {
            get
            {
                try
                {
                    // Playnite turns GameAdditionAllowed off for each of its runs and ProgressActive on while its bar is up.
                    var model = Model;
                    return Get(model, "GameAdditionAllowed") as bool? == false
                        || Get(model, "ProgressActive") as bool? == true
                        || TaskHandler?.GetProperty("IsActive", Statics)?.GetValue(null) as bool? == true;
                }
                catch (Exception e)
                {
                    logger.Warn(e, "Couldn't tell whether Playnite's library is busy");
                    return false;
                }
            }
        }

        public void UpdateLibrary(Action<IReadOnlyList<Guid>> done)
        {
            OnUiThread(async () =>
            {
                var added = new List<Guid>();
                EventHandler<ItemCollectionChangedEventArgs<Game>> collect = (_, e) =>
                {
                    lock (added)
                    {
                        added.AddRange(e.AddedItems.Select(g => g.Id));
                    }
                };

                try
                {
                    // Every integration's library, no emulated games, no metadata: that is DownloadMetadata's, after.
                    // Playnite's UpdateLibrary returns no list of what it added, so the database's own adds are counted.
                    var model = Model;
                    var update = model?.GetType().GetMethod("UpdateLibrary", new[] { typeof(bool), typeof(bool), typeof(bool) });
                    if (update == null)
                    {
                        logger.Warn("Can't update the library: this Playnite build has no UpdateLibrary(bool, bool, bool)");
                        return;
                    }

                    api.Database.Games.ItemCollectionChanged += collect;
                    try
                    {
                        await (Task)update.Invoke(model, new object[] { false, true, false });
                    }
                    finally
                    {
                        api.Database.Games.ItemCollectionChanged -= collect;
                    }

                    // Cancelled part way: none, as the contract says, though some may have been added.
                    if (CancelToken()?.IsCancellationRequested == true)
                    {
                        logger.Info("Library update was cancelled");
                        added.Clear();
                    }
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't update the library");
                    added.Clear();
                }
                finally
                {
                    Call(() => done(added.Distinct().ToList()));
                }
            });
        }

        public void DownloadMetadata(IReadOnlyList<Guid> games, Action done)
        {
            OnUiThread(async () =>
            {
                try
                {
                    await Download(games.Select(api.Database.Games.Get).Where(g => g != null).ToList());
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't download metadata");
                }
                finally
                {
                    Call(done);
                }
            });
        }

        public void DownloadMissingMetadata(Action done)
        {
            OnUiThread(async () =>
            {
                try
                {
                    var fields = MetadataSettings();
                    if (fields == null)
                    {
                        logger.Warn("Can't download missing metadata: this Playnite build has no AppSettings.MetadataSettings");
                        return;
                    }

                    var missing = api.Database.Games.Where(g => !g.Hidden && IsMissingMetadata(g, fields)).ToList();
                    logger.Info($"{missing.Count} games are missing metadata");
                    await Download(missing);
                }
                catch (Exception e)
                {
                    logger.Error(e, "Couldn't download missing metadata");
                }
                finally
                {
                    Call(done);
                }
            });
        }

        public void CancelRun()
        {
            OnUiThread(() =>
            {
                // As the progress bar's own Cancel: cancels GlobalTaskHandler's token, which our download uses too.
                var model = Model;
                var cancel = model?.GetType().GetMethod("CancelProgress", Type.EmptyTypes);
                if (cancel == null)
                {
                    logger.Warn("Can't cancel Playnite's progress: this Playnite build has no CancelProgress");
                    return;
                }

                cancel.Invoke(model, null);
            });
        }

        /// <summary>
        /// Whether <paramref name="game"/> has a blank field among those Playnite's saved metadata settings import (Import
        /// on), so a download could fill it. Install size isn't one: it's measured, not looked up.
        /// </summary>
        private static bool IsMissingMetadata(Game game, object settings)
        {
            bool Imports(string field) => Get(Get(settings, field), "Import") as bool? == true;
            bool None<T>(ICollection<T> items) => items == null || items.Count == 0;

            return (Imports("Name") && string.IsNullOrEmpty(game.Name))
                || (Imports("Description") && string.IsNullOrEmpty(game.Description))
                || (Imports("CoverImage") && string.IsNullOrEmpty(game.CoverImage))
                || (Imports("BackgroundImage") && string.IsNullOrEmpty(game.BackgroundImage))
                || (Imports("Icon") && string.IsNullOrEmpty(game.Icon))
                || (Imports("Genre") && None(game.GenreIds))
                || (Imports("Developer") && None(game.DeveloperIds))
                || (Imports("Publisher") && None(game.PublisherIds))
                || (Imports("Tag") && None(game.TagIds))
                || (Imports("Feature") && None(game.FeatureIds))
                || (Imports("Series") && None(game.SeriesIds))
                || (Imports("AgeRating") && None(game.AgeRatingIds))
                || (Imports("Region") && None(game.RegionIds))
                || (Imports("Platform") && None(game.PlatformIds))
                || (Imports("Links") && None(game.Links))
                || (Imports("ReleaseDate") && game.ReleaseDate == null)
                || (Imports("CriticScore") && game.CriticScore == null)
                || (Imports("CommunityScore") && game.CommunityScore == null);
        }

        /// <summary>Playnite's saved metadata settings, AppSettings.MetadataSettings, those of Desktop's Download Metadata dialog.</summary>
        private static object MetadataSettings() => Get(Get(Model, "AppSettings"), "MetadataSettings");

        /// <summary>
        /// Desktop's <c>DesktopAppViewModel.DownloadMetadata</c>, rebuilt against Fullscreen's main view model: Playnite's
        /// <c>MetadataDownloader</c> over <paramref name="games"/> with the progress bar and GlobalTaskHandler's token, so
        /// the bar's own Cancel stops it. From a copy of the saved settings that skips every field already set.
        /// </summary>
        private static async Task Download(List<Game> games)
        {
            if (games.Count == 0)
            {
                return;
            }

            var model = Model;
            var appSettings = Get(model, "AppSettings");
            var extensions = Get(model, "Extensions");
            var database = Get(model, "Database");
            var filters = Get(model, "DatabaseFilters");
            var handler = TaskHandler;
            var downloaderType = PlayniteType("Playnite.Metadata.MetadataDownloader");
            var download = downloaderType?.GetMethod("DownloadMetadataAsync");
            var settings = SkippingSetFields(Get(appSettings, "MetadataSettings"));
            var progressTask = handler?.GetField("ProgressTask", Statics);
            var cancelToken = handler?.GetProperty("CancelToken", Statics);
            var cancelAndWait = handler?.GetMethod("CancelAndWaitAsync", Statics, null, Type.EmptyTypes, null);
            if (appSettings == null || extensions == null || database == null || filters == null || download == null
                || settings == null || progressTask == null || cancelToken == null || cancelAndWait == null)
            {
                logger.Warn("Can't download metadata: this Playnite build lacks part of Desktop's Download Metadata");
                return;
            }

            var ctorArgs = new[] { database, Get(extensions, "MetadataPlugins"), Get(extensions, "LibraryPlugins") };
            var ctor = downloaderType.GetConstructors().FirstOrDefault(c => Accepts(c.GetParameters(), ctorArgs));
            if (ctor == null)
            {
                logger.Warn("Can't download metadata: this Playnite build has no MetadataDownloader(database, metadata plugins, library plugins)");
                return;
            }

            logger.Info($"Downloading metadata for {games.Count} games, filling blank fields only");
            Set(model, "GameAdditionAllowed", false);
            try
            {
                // Whatever had Playnite's bar is stopped first, as Desktop does.
                await (Task)cancelAndWait.Invoke(null, null);

                Set(filters, "IgnoreDatabaseUpdates", true);
                var cancel = new CancellationTokenSource();
                cancelToken.SetValue(null, cancel);
                var status = ResourceProvider.GetString("LOCProgressMetadata");
                Set(model, "ProgressActive", true);
                Set(model, "ProgressValue", 0d);
                Set(model, "ProgressTotal", (double)games.Count);
                Set(model, "ProgressStatus", status);

                var dispatcher = Application.Current.Dispatcher;
                Action<Game, int, int> progress = (game, i, total) => dispatcher.BeginInvoke(new Action(() =>
                {
                    Set(model, "ProgressValue", (double)(i + 1));
                    Set(model, "ProgressStatus", $"{status} [{i + 1}/{total}]");
                }));

                var downloader = ctor.Invoke(ctorArgs);
                using (downloader as IDisposable)
                {
                    var task = (Task)download.Invoke(downloader, new object[] { games, settings, appSettings, progress, cancel.Token });
                    progressTask.SetValue(null, task);
                    await task;
                }
            }
            finally
            {
                Set(model, "ProgressActive", false);
                Set(model, "GameAdditionAllowed", true);
                Set(filters, "IgnoreDatabaseUpdates", false);
            }
        }

        private static bool Accepts(ParameterInfo[] parameters, object[] args) =>
            parameters.Length == args.Length
            && parameters.Zip(args, (p, a) => a != null && p.ParameterType.IsInstanceOfType(a)).All(ok => ok);

        /// <summary>
        /// A copy of Playnite's saved metadata settings, its sources and fields, with SkipExistingValues forced on: blank
        /// fields are filled and a set one is never replaced, whatever the saved settings say. The saved ones are left as
        /// they are. Null if they can't be copied.
        /// </summary>
        private static object SkippingSetFields(object saved)
        {
            if (saved == null)
            {
                return null;
            }

            // The SDK's JSON is what Playnite saves these settings with.
            var fromJson = typeof(Serialization).GetMethods(Statics)
                .First(m => m.Name == nameof(Serialization.FromJson) && m.IsGenericMethod && m.GetParameters().Length == 1)
                .MakeGenericMethod(saved.GetType());
            var copy = fromJson.Invoke(null, new object[] { Serialization.ToJson(saved) });
            var skip = copy?.GetType().GetProperty("SkipExistingValues");
            if (skip == null)
            {
                return null;
            }

            skip.SetValue(copy, true);
            return copy;
        }

        private static CancellationTokenSource CancelToken() =>
            TaskHandler?.GetProperty("CancelToken", Statics)?.GetValue(null) as CancellationTokenSource;

        /// <summary>Runs <paramref name="action"/> on the UI thread once the caller is done, logging what it throws.</summary>
        private static void OnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                logger.Warn("Can't reach Playnite's UI thread: no application");
                Call(action);
                return;
            }

            dispatcher.BeginInvoke(new Action(() => Call(action)));
        }

        /// <summary><see cref="OnUiThread(Action)"/> for an async run; it catches its own failures and calls back itself.</summary>
        private static void OnUiThread(Func<Task> run) => OnUiThread(() => { _ = run(); });

        private static void Call(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                logger.Error(e, "A library task's callback failed");
            }
        }
    }
}
