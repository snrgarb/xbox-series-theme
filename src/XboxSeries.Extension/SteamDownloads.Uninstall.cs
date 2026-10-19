using System;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Uninstalling Steam games through the running client, the way <see cref="Install"/> installs them, and marking
    /// them not installed in Playnite once Steam's manifest says it has finished.
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        // StateFlags' Uninstalled value (Steam's EAppState StateUninstalled): the whole of the flags for an app Steam
        // knows but has no files of, as a manifest kept after its uninstall can read.
        private const long StateUninstalled = 1;

        // What Uninstall asks the running client: app_uninstall into its console over CEF, or -silent -console
        // +app_uninstall handed to steam.exe, followed by the app id either way. Without -complete, as the client's
        // own Uninstall (steam://uninstall, the Steam plugin's route) removes the game's folder and manifest and keeps
        // what Steam holds for the account, its cloud saves among them; what -complete takes beyond that Valve doesn't
        // say, so it's left off until a game uninstalled without it is seen to leave something behind that matters.
        // -console is the issue's (#79) form, which the forum's workaround for app_uninstall uses: seen on the TV to
        // raise no window, no confirmation and no Console page.
        private readonly SteamCommand uninstalls = new SteamCommand("uninstall", "app_uninstall", "-silent -console +app_uninstall");

        private bool uninstallMarkWarned;

        /// <summary>
        /// Only for a game Steam has a manifest for: <see cref="MarkUninstalled"/> takes no manifest for Steam having
        /// finished, so a game whose manifest can't be found (its library unplugged, or missing from
        /// libraryfolders.vdf) would read uninstalled a second after asking, whatever Steam did. Playnite's own
        /// Uninstall stands for it.
        /// </summary>
        public bool CanUninstall(Guid gameId)
        {
            try
            {
                var appId = AppIdOf(gameId);
                return appId != null && SteamExe() != null && ManifestOf(appId.Value) != null;
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref uninstalls.Warned, e, "Couldn't find Steam to uninstall with");
                return false;
            }
        }

        /// <summary>
        /// Asks Steam to uninstall the game, by the same two routes as <see cref="Install"/>, and not waited for: Steam
        /// is followed from its manifests instead, through <see cref="MarkUninstalled"/>.
        /// <para>
        /// When Steam is running and the user has its CEF debugger on, <c>app_uninstall</c> is typed into the client's
        /// own console over that debugger: the route expected to raise no Steam window and no confirmation, as
        /// <c>app_install</c> raises none there, the console taking the command as typed rather than through the
        /// Uninstall button's dialog.
        /// </para>
        /// <para>
        /// Otherwise <c>-silent -console +app_uninstall</c> goes to steam.exe, which passes it to the running client
        /// and exits, or starts Steam with it, <c>-silent</c> keeping the main window down as it does for an install,
        /// though its sign-in or update windows can still show. Steam's open windows are sent to the tray first and
        /// Playnite is kept in front after, as for an install. Tried on the TV, this route raised no Steam window, no
        /// confirmation and no Console page, and Playnite kept the foreground.
        /// </para>
        /// </summary>
        public void Uninstall(Guid gameId)
        {
            string exe;
            uint? appId;
            try
            {
                appId = AppIdOf(gameId);
                exe = appId == null ? null : SteamExe();
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref uninstalls.Warned, e, $"Couldn't find Steam to uninstall game {gameId} with");
                return;
            }

            if (exe != null)
            {
                AskSteamApp(exe, appId.Value, uninstalls, $"game {gameId}");
            }
        }

        /// <summary>
        /// Ends as Playnite's own uninstall does once the Steam plugin's watch sees the game gone: not installed, not
        /// uninstalling, and with no install folder. Steam is done with the game once it has no manifest for it, as
        /// after a whole uninstall, or one whose StateFlags read <see cref="StateUninstalled"/> alone; while the
        /// Uninstalling bit is set, or the manifest reads anything else, it isn't, and the game is left: a manifest
        /// mid-write reads 0, so it waits for the next call. Asked once a second while the core follows the uninstall,
        /// so a manifest already gone has library folders reread at most every few seconds, as <see cref="ManifestOf"/>
        /// allows.
        /// </summary>
        public void MarkUninstalled(Guid gameId)
        {
            try
            {
                var appId = AppIdOf(gameId);
                var game = api.Database.Games.Get(gameId);
                if (appId == null || game == null || !game.IsInstalled)
                {
                    return;
                }

                var manifest = ManifestOf(appId.Value);
                if (manifest != null && Number(Values(manifest, "StateFlags"), "StateFlags") != StateUninstalled)
                {
                    return;
                }

                game.IsInstalled = false;
                game.IsUninstalling = false;
                game.InstallDirectory = string.Empty;
                api.Database.Games.Update(game);
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref uninstallMarkWarned, e, $"Couldn't mark game {gameId} uninstalled from Steam's manifest");
            }
        }

        public void UninstallUnfinished(Guid gameId)
        {
            logger.Warn($"Steam still hadn't uninstalled game {gameId} (app {AppIdOf(gameId)}) when the extension stopped following it");
        }
    }
}
