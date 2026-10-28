using Microsoft.Win32;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// The current user's registry (HKCU), as setup reads and writes it: each value a string, a DWORD as an int, or
    /// binary as a byte[]; null for no value.
    /// </summary>
    internal interface IRegistry
    {
        object? Get(string key, string name);

        void Set(string key, string name, object value);

        void Delete(string key, string name);
    }

    internal sealed class UserRegistry : IRegistry
    {
        public object? Get(string key, string name)
        {
            using var opened = Registry.CurrentUser.OpenSubKey(key);
            return opened?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        }

        public void Set(string key, string name, object value)
        {
            using var opened = Registry.CurrentUser.CreateSubKey(key);
            opened.SetValue(name, value, value switch
            {
                int => RegistryValueKind.DWord,
                byte[] => RegistryValueKind.Binary,
                _ => RegistryValueKind.String,
            });
        }

        public void Delete(string key, string name)
        {
            using var opened = Registry.CurrentUser.OpenSubKey(key, writable: true);
            opened?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}
