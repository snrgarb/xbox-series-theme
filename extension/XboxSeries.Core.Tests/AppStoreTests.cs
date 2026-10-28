using System;
using Xunit;

namespace XboxSeries.Core.Tests
{
    /// <summary>An App is a Microsoft Store app when it is started as Playnite's Store import starts one.</summary>
    public class AppStoreTests
    {
        private static App AppOf(string path, string arguments) =>
            new App(Guid.NewGuid(), "App", path, arguments);

        [Fact]
        public void An_App_Playnite_imported_from_the_Store_is_a_Store_app()
        {
            var app = AppOf("explorer.exe", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App");

            Assert.True(app.IsStoreApp);
        }

        [Theory]
        [InlineData(@"C:\Windows\explorer.exe", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
        [InlineData("EXPLORER.EXE", @"SHELL:APPSFOLDER\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
        public void A_Store_app_may_name_explorer_by_its_full_path_and_in_any_case(string path, string arguments)
        {
            Assert.True(AppOf(path, arguments).IsStoreApp);
        }

        [Theory]
        [InlineData(@"  shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App  ")]
        [InlineData(@"""shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App""")]
        [InlineData(@" ""shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"" ")]
        public void A_Store_apps_argument_may_be_padded_or_quoted(string arguments)
        {
            Assert.True(AppOf("explorer.exe", arguments).IsStoreApp);
        }

        [Theory]
        [InlineData(@"C:\Steam\steam.exe", "-bigpicture")]
        [InlineData(@"C:\Steam\steam.exe", null)]
        [InlineData(@"C:\Tools\notexplorer.exe", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
        [InlineData("explorer.exe", @"C:\Games")]
        [InlineData("explorer.exe", @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe")]
        [InlineData("explorer.exe", "shell:AppsFolder")]
        [InlineData("explorer.exe", null)]
        [InlineData(null, @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
        public void Any_other_App_is_not_a_Store_app(string path, string arguments)
        {
            Assert.False(AppOf(path, arguments).IsStoreApp);
        }
    }
}
