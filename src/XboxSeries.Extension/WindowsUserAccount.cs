using System;
using System.IO;
using System.Security;
using System.Security.Principal;
using Microsoft.Win32;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>Adapter: the Windows user signed in, with the account picture Settings shows for them.</summary>
    internal sealed class WindowsUserAccount : IUserAccount
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public string Name => Environment.UserName;

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
    }
}
