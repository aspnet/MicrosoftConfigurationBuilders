using Microsoft.Configuration.ConfigurationBuilders;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace Test
{
    public class UtilsTests
    {
        [Fact]
        public void Utils_MapPath()
        {
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
