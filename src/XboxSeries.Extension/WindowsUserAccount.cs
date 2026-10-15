using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: the Windows user signed in, with the account picture and full name the first profile takes, and the
    /// Gamerpics folder in the extension's data folder, where the user puts pictures a profile can wear.
    /// </summary>
    internal sealed class WindowsUserAccount : IUserAccount
    {
        private const int NameDisplay = 3;
        private const int ErrorMoreData = 234;

        private static readonly ILogger logger = LogManager.GetLogger();

        private static readonly string[] PictureExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".bmp" };

        private readonly string gamerpicsFolder;
        private readonly Lazy<string> realName = new Lazy<string>(ReadRealName);

        /// <param name="dataFolder">The extension's own data folder, which holds the Gamerpics folder.</param>
        public WindowsUserAccount(string dataFolder)
        {
            gamerpicsFolder = Path.Combine(dataFolder, "Gamerpics");
        }

        public string Name => Environment.UserName;

        /// <summary>
        /// The full name Windows records for the user: a local or Microsoft account's from this PC's own accounts, which
        /// Windows fills in from the Microsoft account; a domain user's from the domain. Empty when there is none or it
        /// can't be read.
        /// </summary>
        public string RealName => realName.Value;

        /// <summary>
        /// The images in the Gamerpics folder, by name, read afresh each time. The folder is made if it's missing, so the
        /// user can find it.
        /// </summary>
        public IReadOnlyList<string> Gamerpics
        {
            get
            {
                try
                {
                    Directory.CreateDirectory(gamerpicsFolder);
                    return Directory.EnumerateFiles(gamerpicsFolder)
                        .Where(f => PictureExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                        .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is SecurityException || e is ArgumentException || e is NotSupportedException)
                {
                    logger.Warn(e, "Couldn't read the Gamerpics folder");
                    return new string[0];
                }
            }
        }

        /// <summary>
        /// The account picture at 192 px, which Windows records per user under AccountPicture; Windows' default user
        /// picture for a user who never set one.
        /// </summary>
        public string Picture
        {
            get
            {
                try
                {
                    var sid = WindowsIdentity.GetCurrent().User?.Value;
                    using (var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\AccountPicture\Users\{sid}"))
                    {
                        if (key?.GetValue("Image192") is string picture && File.Exists(picture))
                        {
                            return picture;
                        }
                    }
                }
                catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException || e is IOException)
                {
                    logger.Warn(e, "Couldn't read the Windows account picture");
                }

                var fallback = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    @"Microsoft\User Account Pictures\user-192.png");
                return File.Exists(fallback) ? fallback : null;
            }
        }

        /// <summary>This PC's own full name for the user, else the domain's display name; empty on any failure, never throwing.</summary>
        private static string ReadRealName()
        {
            try
            {
                return LocalFullName() ?? DomainDisplayName() ?? "";
            }
            catch (Exception e)
            {
                logger.Warn(e, "Couldn't read the Windows user's full name");
                return "";
            }
        }

        /// <summary>
        /// USER_INFO_10's full name for the user, from this PC's own accounts, a Microsoft account's among them; null when
        /// it has none, or for a domain user, whom this PC doesn't know.
        /// </summary>
        private static string LocalFullName()
        {
            if (NetUserGetInfo(null, Environment.UserName, 10, out var buffer) != 0 || buffer == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                var info = (UserInfo10)Marshal.PtrToStructure(buffer, typeof(UserInfo10));
                return string.IsNullOrWhiteSpace(info.FullName) ? null : info.FullName.Trim();
            }
            finally
            {
                NetApiBufferFree(buffer);
            }
        }

        /// <summary>The domain's display name for a domain user; null for anyone else, or when it can't be had.</summary>
        private static string DomainDisplayName()
        {
            uint size = 256;
            var name = new StringBuilder((int)size);
            if (!GetUserNameEx(NameDisplay, name, ref size))
            {
                if (Marshal.GetLastWin32Error() != ErrorMoreData)
                {
                    return null;
                }

                name = new StringBuilder((int)size);
                if (!GetUserNameEx(NameDisplay, name, ref size))
                {
                    return null;
                }
            }

            var display = name.ToString().Trim();
            return display.Length == 0 ? null : display;
        }

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int NetUserGetInfo(string serverName, string userName, int level, out IntPtr buffer);

        [DllImport("netapi32.dll")]
        private static extern int NetApiBufferFree(IntPtr buffer);

        [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserNameEx(int nameFormat, StringBuilder name, ref uint size);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct UserInfo10
        {
            public string Name;
            public string Comment;
            public string UserComment;
            public string FullName;
        }
    }
}
