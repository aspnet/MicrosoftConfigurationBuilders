using System;
using System.IO;
using System.Reflection;
using Microsoft.Configuration.ConfigurationBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class UtilsTests
    {
        [TestMethod]
        public void Utils_MapPath()
        {
            // Absolute paths
            Assert.AreEqual(Utils.MapPath(@"C:\Windows\System32"), Path.GetFullPath(@"C:\Windows\System32"));
            Assert.AreEqual(Utils.MapPath(@"/Windows"), Path.GetFullPath(@"/Windows"));

            // Relative paths
            Assert.AreEqual(Utils.MapPath(@"foo\bar\baz"), Path.GetFullPath(@"foo\bar\baz"));
            Assert.AreEqual(Utils.MapPath(@"..\foo\..\baz"), Path.GetFullPath(@"..\foo\..\baz"));

            // Home-relative paths
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Assert.AreEqual(Utils.MapPath(@"~\"), baseDir);
            Assert.AreEqual(Utils.MapPath(@"~/"), baseDir);
            Assert.AreEqual(Utils.MapPath(@"~/hello"), Path.Combine(baseDir, @"hello"));
            Assert.AreEqual(Utils.MapPath(@"~\good-bye\"), Path.Combine(baseDir, @"good-bye\"));
        }

        [TestMethod]
        public void Utils_MapPath_AspNet()
        {
            // Fake running in ASP.Net
            FakeAspNet(true);

            // Since HostingEnvironment is not actually loaded, Utils.ServerMapPath should spit our string right back at us.
            string badPath = ")*@#__This_is_not_a_valid_Path_and_will_error_Unless_we_Get_into_ServerMapPath()}}}}!";
            Assert.AreEqual(Utils.MapPath(badPath), badPath,
                $"Utils_MapPath_AspNet: Did not enter ServerMapPath()");

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
