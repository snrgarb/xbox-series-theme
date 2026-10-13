using System.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    public class CoreBoundaryTests
    {
        [Fact]
        public void Core_does_not_reference_Playnite()
        {
            var references = typeof(ExtensionCore).Assembly.GetReferencedAssemblies().Select(a => a.Name);

            Assert.DoesNotContain(references, name => name.StartsWith("Playnite"));
        }
    }
}
