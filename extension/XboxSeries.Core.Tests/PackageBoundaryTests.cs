using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace XboxSeries.Core.Tests
{
    // One folder per package at the root: bootshell/, extension/ and theme/. A project reaches only into its own package's
    // folder and shared/, where whatever two packages both need lives.
    public class PackageBoundaryTests
    {
        private static readonly string RepoRoot = FindRepoRoot();
        private static readonly string[] Packages = { "bootshell", "extension", "theme" };

        [Fact]
        public void No_project_reaches_into_another_package()
        {
            var crossings =
                from package in Packages
                from project in Directory.GetFiles(Path.Combine(RepoRoot, package), "*.*proj", SearchOption.AllDirectories)
                where !project.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                from item in XDocument.Load(project).Descendants()
                let include = (string)item.Attribute("Include")
                where include != null && (include.Contains("\\") || include.Contains("/"))
                let path = Resolve(Path.GetDirectoryName(project), include)
                where !IsUnder(path, package) && !IsUnder(path, "shared")
                select $"{project.Substring(RepoRoot.Length + 1)}: {include}";

            Assert.Empty(crossings);
        }

        // The folder an item's path names, up to its first wildcard; null, counted as a crossing, if it names a property not
        // resolved here.
        private static string Resolve(string projectDir, string include)
        {
            var path = include.Replace("$(RepoRoot)", RepoRoot + Path.DirectorySeparatorChar)
                .Replace("$(MSBuildThisFileDirectory)", projectDir + Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar);
            if (path.Contains("$("))
            {
                return null;
            }
            var wildcard = path.IndexOf('*');
            var folder = path.Substring(0, path.LastIndexOf(Path.DirectorySeparatorChar, wildcard < 0 ? path.Length - 1 : wildcard) + 1);
            return Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(projectDir, folder));
        }

        private static bool IsUnder(string path, string folder) =>
            path != null && path.StartsWith(Path.Combine(RepoRoot, folder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XboxSeries.slnx")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName ?? throw new InvalidOperationException("Repo root not found");
        }
    }
}
