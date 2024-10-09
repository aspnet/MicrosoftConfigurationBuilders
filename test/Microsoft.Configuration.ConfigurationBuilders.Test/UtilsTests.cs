using System;
using System.IO;
using System.Reflection;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class UtilsTests
    {
        [Fact]
        public void Utils_MapPath()
        {
            // Not running in ASP.Net
            var exception = Record.Exception(() => Utils.MapPath("\"{Very*invalid\\..\\..\\..\\%path&"));
            Assert.NotNull(exception);

            // Rooted paths don't change
            Assert.Equal(@"C:\Windows\System32", Utils.MapPath(@"C:\Windows\System32"));
            Assert.Equal(@"/Windows", Utils.MapPath(@"/Windows"));
            Assert.Equal(@"\Windows", Utils.MapPath(@"\Windows"));

            // Relative paths
            Assert.Equal(Path.GetFullPath(@"foo\bar\baz"), Utils.MapPath(@"foo\bar\baz"));
            Assert.Equal(Path.GetFullPath(@"..\foo\..\baz"), Utils.MapPath(@"..\foo\..\baz"));

            // Home-relative paths
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Assert.Equal(baseDir, Utils.MapPath(@"~\"));
            Assert.Equal(baseDir, Utils.MapPath(@"~/"));
            Assert.Equal(Path.Combine(baseDir, @"hello"), Utils.MapPath(@"~/hello"));
            Assert.Equal(Path.Combine(baseDir, @"good-bye\"), Utils.MapPath(@"~\good-bye\"));
        }

        [Fact]
        public void Utils_MapPath_AspNet()
        {
            try
            {
                // Fake running in ASP.Net
                FakeAspNet(true);

                // Rooted paths don't change
                Assert.Equal(@"C:\Windows\System32", Utils.MapPath(@"C:\Windows\System32"));
                Assert.Equal(@"/Windows", Utils.MapPath(@"/Windows"));
                Assert.Equal(@"\Windows", Utils.MapPath(@"\Windows"));

                // A real HostingEnvironment.MapPath would reject this. But we don't mock all that logic.
                // Instead, just verify that we have gone through the ASP.Net HostingEnvironment path.
                string badPath = ")*@#__This_is_not_a_valid_Path_and_will_error_Unless_we_Get_into_ServerMapPath()}}}}!";
                Assert.Equal(MockHostingEnvironment.PathPrefix + badPath, Utils.MapPath(badPath));
            }
            finally
            {
                // Stop faking ASP.Net
                FakeAspNet(false);
            }
        }

        [Fact]
        public void Utils_MapPath_OpenConfig()
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;

            // Not running in ASP.Net
            var exception = Record.Exception(() => Utils.MapPath("\"{Very*invalid\\..\\..\\..\\%path&", appSettings));
            Assert.NotNull(exception);

            // Rooted paths don't change
            Assert.Equal(@"C:\Windows\System32", Utils.MapPath(@"C:\Windows\System32", appSettings));
            Assert.Equal(@"/Windows", Utils.MapPath(@"/Windows", appSettings));
            Assert.Equal(@"\Windows", Utils.MapPath(@"\Windows", appSettings));

            // Relative paths are relative to the config file
            string configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testConfigFiles");
            Assert.Equal(Path.GetFullPath(Path.Combine(configDir, @"foo\bar\baz")), Utils.MapPath(@"foo\bar\baz", appSettings));
            Assert.Equal(Path.GetFullPath(Path.Combine(configDir, @"..\foo\..\baz")), Utils.MapPath(@"..\foo\..\baz", appSettings));

            // Home-relative paths
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Assert.Equal(baseDir, Utils.MapPath(@"~\", appSettings));
            Assert.Equal(baseDir, Utils.MapPath(@"~/", appSettings));
            Assert.Equal(Path.Combine(baseDir, @"hello"), Utils.MapPath(@"~/hello", appSettings));
            Assert.Equal(Path.Combine(baseDir, @"good-bye\"), Utils.MapPath(@"~\good-bye\", appSettings));
        }

        private void FakeAspNet(bool isAspNet)
        {
            // Make sure Utils is static inited.
            Utils.MapPath(@"\");

            // Set that IsAspNet flag appropriately
            Type utils = typeof(Utils);
            FieldInfo isAspNetField = utils.GetField("s_isAspNet", BindingFlags.Static | BindingFlags.NonPublic);
            isAspNetField.SetValue(null, isAspNet);

            // And mock HostingEnvironment?
            FieldInfo hostingEnvironment = utils.GetField("s_hostingEnvironmentType", BindingFlags.Static | BindingFlags.NonPublic);
            hostingEnvironment.SetValue(null, isAspNet ? typeof(MockHostingEnvironment) : null);
        }

        private class MockHostingEnvironment
        {
            public static string PathPrefix = @"\\MockHE\ASP.Net\";
            public static string MapPath(string path)
            {
                return PathPrefix + path;
            }
        }
    }
}
