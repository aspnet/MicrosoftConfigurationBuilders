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
            // Absolute paths
            Assert.Equal(Utils.MapPath(@"C:\Windows\System32"), Path.GetFullPath(@"C:\Windows\System32"));
            Assert.Equal(Utils.MapPath(@"/Windows"), Path.GetFullPath(@"/Windows"));

            // Relative paths
            Assert.Equal(Utils.MapPath(@"foo\bar\baz"), Path.GetFullPath(@"foo\bar\baz"));
            Assert.Equal(Utils.MapPath(@"..\foo\..\baz"), Path.GetFullPath(@"..\foo\..\baz"));

            // Home-relative paths
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Assert.Equal(Utils.MapPath(@"~\"), baseDir);
            Assert.Equal(Utils.MapPath(@"~/"), baseDir);
            Assert.Equal(Utils.MapPath(@"~/hello"), Path.Combine(baseDir, @"hello"));
            Assert.Equal(Utils.MapPath(@"~\good-bye\"), Path.Combine(baseDir, @"good-bye\"));
        }

        [Fact]
        public void Utils_MapPath_AspNet()
        {
            // Fake running in ASP.Net
            FakeAspNet(true);

            // Since HostingEnvironment is not actually loaded, Utils.ServerMapPath should spit our string right back at us.
            string badPath = ")*@#__This_is_not_a_valid_Path_and_will_error_Unless_we_Get_into_ServerMapPath()}}}}!";
            Assert.Equal(Utils.MapPath(badPath), badPath);

            // Stop faking ASP.Net
            FakeAspNet(false);
        }

        private void FakeAspNet(bool isAspNet)
        {
            // Make sure Utils is static inited.
            Utils.MapPath(@"\");

            // Utils also depends on HostingEnvironment to do the map path
            //bool whoCares = System.Web.Hosting.HostingEnvironment.IsHosted;

            // Set that IsAspNet flag appropriately
            Type utils = typeof(Utils);
            FieldInfo isAspNetField = utils.GetField("s_isAspNet", BindingFlags.Static | BindingFlags.NonPublic);
            isAspNetField.SetValue(null, isAspNet);
        }
    }
}
