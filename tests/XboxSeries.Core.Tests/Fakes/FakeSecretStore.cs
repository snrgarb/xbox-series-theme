using System.Collections.Generic;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>The encrypted store, as a dictionary a test can fill beforehand and look into afterwards.</summary>
    public sealed class FakeSecretStore : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        public string Read(string name) => Values.TryGetValue(name, out var value) ? value : null;

        public void Write(string name, string value) => Values[name] = value;

        public void Delete(string name) => Values.Remove(name);
    }
}
