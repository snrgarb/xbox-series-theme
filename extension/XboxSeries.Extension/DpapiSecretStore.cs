using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: values encrypted for the Windows user with DPAPI (CurrentUser), all in one file in the extension's data
    /// folder. That keeps them from other Windows users, not from other programs running as this one. A file that can't
    /// be decrypted, written by another user or machine or corrupt, reads as empty and is replaced on the next write.
    /// </summary>
    internal sealed class DpapiSecretStore : ISecretStore
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>Ties the encrypted file to this use, so another DPAPI blob of the user's can't stand in for it.</summary>
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("XboxSeries.Extension.DpapiSecretStore");

        private readonly object gate = new object();
        private readonly string file;
        private Dictionary<string, string> values;

        public DpapiSecretStore(string dataDirectory)
        {
            file = Path.Combine(dataDirectory, "discord-secrets.dat");
        }

        public string Read(string name)
        {
            lock (gate)
            {
                return Values.TryGetValue(name, out var value) ? value : null;
            }
        }

        public void Write(string name, string value)
        {
            if (value == null)
            {
                Delete(name);
                return;
            }

            lock (gate)
            {
                Values[name] = value;
                Save();
            }
        }

        public void Delete(string name)
        {
            lock (gate)
            {
                if (Values.Remove(name))
                {
                    Save();
                }
            }
        }

        /// <summary>The file's values, read the first time they're needed.</summary>
        private Dictionary<string, string> Values => values ?? (values = Load());

        private Dictionary<string, string> Load()
        {
            if (!File.Exists(file))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                var plain = ProtectedData.Unprotect(File.ReadAllBytes(file), Entropy, DataProtectionScope.CurrentUser);
                return JsonConvert.DeserializeObject<Dictionary<string, string>>(Encoding.UTF8.GetString(plain))
                    ?? new Dictionary<string, string>();
            }
            catch (Exception e) when (e is CryptographicException || e is IOException || e is UnauthorizedAccessException || e is JsonException)
            {
                logger.Warn(e, $"Couldn't read the saved Discord secrets in {file}; starting without them");
                return new Dictionary<string, string>();
            }
        }

        private void Save()
        {
            try
            {
                var plain = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(values));
                var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);

                // Written whole to a temporary file first, so a crash mid-write never leaves half a file.
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                var temporary = file + ".tmp";
                File.WriteAllBytes(temporary, encrypted);
                if (File.Exists(file))
                {
                    File.Replace(temporary, file, null);
                }
                else
                {
                    File.Move(temporary, file);
                }
            }
            catch (Exception e) when (e is CryptographicException || e is IOException || e is UnauthorizedAccessException)
            {
                logger.Error(e, $"Couldn't save the Discord secrets to {file}");
            }
        }
    }
}
