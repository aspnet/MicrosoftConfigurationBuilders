using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class RecursionTests
    {
        // ======================================================================
        //   Instance Check and Reentry
        //      (I guess these are a little anti-pattern-y for unit tests since
        //       they do go into the actual .Net config system rather than
        //       fully mocking everything. Oh well.)
        // ======================================================================
        [Fact]
        public void InstanceCheck()
        {
            // A new instance of each config builder is spun up for each config file,
            // and section. But is reused for the two processing phases (ie ProcessRawXml
            // and ProcessConfigurationSection)
            //
            // We do not depend on this behavior, and it is not guaranteed by the
            // .Net framework. But I wanted to have this sanity check while working
            // on recursion handling, and there's no sense letting a perfectly fine
            // unit test go to waste.
            var config = LoadMultiLevelConfig("instance-machine.config", "instance-appexe.config");

            // Check appSettings
            string guid2 = config.AppSettings.Settings["not-a-guid"]?.Value;
            Assert.Equal(2, config.AppSettings.Settings.Count);
            Assert.NotEqual("I am a string.", guid2);
            string guid1 = config.AppSettings.Settings["InstanceCheck1"]?.Value;
            Assert.True(Guid.TryParse(guid1, out Guid g));

            // Load fakeAppSettings with the same config builder names applied.
            // They are different instances. The guids will be different.
            var fakeAppSettings = (AppSettingsSection)config.GetSection("fakeAppSettings");
            Assert.Equal(2, fakeAppSettings.Settings.Count);
            string fakeGuid2 = fakeAppSettings.Settings["not-a-guid"]?.Value;
            Assert.NotEqual("string from appexe", fakeGuid2);
            Assert.NotEqual(guid2, fakeGuid2);
            string fakeGuid1 = fakeAppSettings.Settings["InstanceCheck1"]?.Value;
            Assert.NotEqual(guid1, fakeGuid1);

            // Load appSettings section another way. It should have been cached, and thus not "built" again. The guids will be the same.
            var appSettings = (AppSettingsSection)config.GetSection("appSettings");
            string guid2Again = config.AppSettings.Settings["not-a-guid"]?.Value;
            Assert.Equal(2, appSettings.Settings.Count);
            Assert.Equal(guid2, guid2Again);
            Assert.Equal(appSettings.Settings["InstanceCheck1"]?.Value, guid1);
        }

        // A config builder will typically never be called with another config builder already on the stack... unless we're getting an appsetting for that
        // first config builder. Typically. Perhaps somebody has a complex scenario that is not typical. But we do watch for recursion, just in case.
        // Because if it happens, it can be difficult to diagnose.
        // 
        // 'Recursion' in this case means executing on the same section in the same config file. 'Re-entry' - ie, executing the same builder* on a
        //      different section is allowed.   (*Same builder definition. It won't ever be the same instance, whether experiencing recursion or simple re-entry.)
        //
        // Possible behaviors when recursion is detected:
        //      1) Throw: [Default] Throw when recursion is detected.
        //      2) Stop: Do not throw when recursion is detected. Instead, stop processing and unwind. Can yield inconsistent* results.
        //      3) None: Do nothing. Recursion is allowed.
        //
        // * Consider the Recursion_Direct test with two config levels:
        //      1: App requests config section.
        //      2: Sys.Cfg starts loading App-level config, which...
        //          a) loads parent machine-level config first, which as a result of our recursive builder...
        //              i) requests config section again, which ends up shortcutting instead of throwing
        //                 when recursion is detected. The result is a config section that looks like it's
        //                 static 'as written' version. This gets cached in Sys.Cfg
        //          b) then loads app-level config, passing the parent machine-level in as a starting point. But...
        //              i) the static version of app-level config was already cached. So this gets returned by
        //                 Sys.Cfg instead of building a new section.
        //      3: The result is an 'as written' view of the config where builders ran, but had no affect on the resulting section.

        [Fact]
        public void Recursion_Parameter()
        {
            // Default is 'Throw'
            var cbDefault = new FakeConfigBuilder();
            cbDefault.Initialize("test", new NameValueCollection());
            Assert.Equal(RecursionGuardValues.Throw, cbDefault.Recursion);

            // Can be set to 'Throw'
            var cbThrow = new FakeConfigBuilder();
            cbThrow.Initialize("test", new NameValueCollection() { { "recur", "Throw" } });
            Assert.Equal(RecursionGuardValues.Throw, cbThrow.Recursion);

            // Can be set to 'Stop'
            var cbStop = new FakeConfigBuilder();
            cbStop.Initialize("test", new NameValueCollection() { { "recur", "Stop" } });
            Assert.Equal(RecursionGuardValues.Stop, cbStop.Recursion);

            // Can be set to 'None'
            var cbNone = new FakeConfigBuilder();
            cbNone.Initialize("test", new NameValueCollection() { { "recur", "Allow" } });
            Assert.Equal(RecursionGuardValues.Allow, cbNone.Recursion);

            // Case Insensitive
            var cbCase = new FakeConfigBuilder();
            cbCase.Initialize("test", new NameValueCollection() { { "recur", "sToP" } });
            Assert.Equal(RecursionGuardValues.Stop, cbCase.Recursion);

            // Unrecognized values throw (including read-from-appSettings syntax)
            var cbBad = new FakeConfigBuilder();
            Assert.Throws<ArgumentException>(() => cbBad.Initialize("test", new NameValueCollection() { { "recur", "Unrecognized" } }));
            var cbAppSettings = new FakeConfigBuilder();
            Assert.Throws<ArgumentException>(() => cbAppSettings.Initialize("test", new NameValueCollection() { { "recur", "${recCheck}" } }));
        }

        [Theory]
        [InlineData(RecursionGuardValues.Throw, null)]
        [InlineData(RecursionGuardValues.Throw, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Stop, null)]
        [InlineData(RecursionGuardValues.Stop, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Allow, null)]
        [InlineData(RecursionGuardValues.Allow, "recursion-appexe.config")]
        public void Recursion_Direct(RecursionGuardValues behavior, string appConfig)
        {
            Environment.SetEnvironmentVariable("_recur_", behavior.ToString());
            var config = LoadMultiLevelConfig("recursion-machine.config", appConfig);

            //
            // Direct Recursion
            //
            try
            {
                var cfgSect = (AppSettingsSection)config.GetSection("direct.recur");
                Assert.False(behavior == RecursionGuardValues.Throw, "recur=Throw should not get to this point in direct.recur test.");
                Assert.NotNull(cfgSect);
                if (appConfig == null)
                {
                    Assert.Equal(3, cfgSect.Settings.Count);
                    Assert.Equal("was here", cfgSect.Settings["DirectRecur"].Value);
                    Assert.Equal("direct.recur", cfgSect.Settings["section"].Value);
                    Assert.Equal("direct.recur", cfgSect.Settings["DirectRecur:section"].Value);
                }
                else
                {
                    Assert.Single(cfgSect.Settings);
                    Assert.Equal("static.recur", cfgSect.Settings["section"].Value);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                Assert.False(behavior == RecursionGuardValues.Stop);

                if (behavior == RecursionGuardValues.Throw)
                {
                    var msg = $"The ConfigurationBuilder 'DirectRecur[Test.RecursiveBuilder]' has recursively re-entered processing of the 'direct.recur' section.";

                    // The main exception should be wrapped, and include our error
                    // message at the top for easy diagnosis
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e, msg);

                    // The exception should be an InvalidOperationException and include
                    // a message about recursion, the section, and the builder.
                    Assert.NotNull(unwrapped);
                    Assert.IsType<InvalidOperationException>(unwrapped);
                    Assert.Equal(msg, unwrapped.Message);
                }
                else if (behavior == RecursionGuardValues.Allow)
                {
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e);

                    // The inner exception should be a "Stack Overflow"
                    Assert.NotNull(unwrapped);
                    Assert.IsType<ConfigBuildersTestException>(unwrapped);
                    Assert.Equal("StackOverflow", unwrapped.Message);
                }
            }
        }

        [Theory]
        [InlineData(RecursionGuardValues.Throw, null)]
        [InlineData(RecursionGuardValues.Throw, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Stop, null)]
        [InlineData(RecursionGuardValues.Stop, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Allow, null)]
        [InlineData(RecursionGuardValues.Allow, "recursion-appexe.config")]
        public void Recursion_Indirect(RecursionGuardValues behavior, string appConfig)
        {
            Environment.SetEnvironmentVariable("_recur_", behavior.ToString());
            var config = LoadMultiLevelConfig("recursion-machine.config", appConfig);

            //
            // Indirect Recursion
            //
            try
            {
                var cfgSect = (AppSettingsSection)config.GetSection("indirect.one");
                Assert.False(behavior == RecursionGuardValues.Throw, "recur=Throw should not get to this point in indirect.one test.");
                Assert.NotNull(cfgSect);
                Assert.Equal("was here", cfgSect.Settings["Indirect1"].Value);
                Assert.Equal("was here", cfgSect.Settings["Indirect1:Indirect2"].Value);
                Assert.Equal("indirect.one", cfgSect.Settings["section"].Value);
                Assert.Equal("indirect.one", cfgSect.Settings["Indirect1:Indirect2:section"].Value);
                if (appConfig == null)
                {
                    Assert.Equal(5, cfgSect.Settings.Count);
                    Assert.Equal("indirect.two", cfgSect.Settings["Indirect1:section"].Value);
                }
                else
                {
                    Assert.Equal(7, cfgSect.Settings.Count);
                    Assert.Equal("indirect.three", cfgSect.Settings["Indirect1:section"].Value);
                    Assert.Equal("was here", cfgSect.Settings["Indirect1:Indirect3"].Value);
                    Assert.Equal("indirect.one", cfgSect.Settings["Indirect1:Indirect3:section"].Value);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                Assert.False(behavior == RecursionGuardValues.Stop);

                if (behavior == RecursionGuardValues.Throw)
                {
                    var msg = $"The ConfigurationBuilder 'Indirect1[Test.RecursiveBuilder]' has recursively re-entered processing of the 'indirect.one' section.";

                    // The main exception should be wrapped, and include our error
                    // message at the top for easy diagnosis
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e, msg);

                    // The exception should be an InvalidOperationException and include
                    // a message about recursion, the section, and the builder.
                    Assert.NotNull(unwrapped);
                    Assert.IsType<InvalidOperationException>(unwrapped);
                    Assert.Equal(msg, unwrapped.Message);
                }
                else if (behavior == RecursionGuardValues.Allow)
                {
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e);

                    // The inner exception should be a "Stack Overflow"
                    Assert.NotNull(unwrapped);
                    Assert.IsType<ConfigBuildersTestException>(unwrapped);
                    Assert.Equal("StackOverflow", unwrapped.Message);
                }
            }
        }

        [Theory]
        [InlineData(RecursionGuardValues.Throw, null)]
        [InlineData(RecursionGuardValues.Throw, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Stop, null)]
        [InlineData(RecursionGuardValues.Stop, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Allow, null)]
        [InlineData(RecursionGuardValues.Allow, "recursion-appexe.config")]
        public void Recursion_False(RecursionGuardValues behavior, string appConfig)
        {
            Environment.SetEnvironmentVariable("_recur_", behavior.ToString());
            var config = LoadMultiLevelConfig("recursion-machine.config", appConfig);

            //
            // No False Recursion
            //
            var cfgSect = (AppSettingsSection)config.GetSection("false.positive");
            Assert.NotNull(cfgSect);
            Assert.Equal("was here", cfgSect.Settings["False1"].Value);
            Assert.Equal("was here", cfgSect.Settings["False2"].Value);
            if (appConfig == null)
            {
                Assert.Equal(3, cfgSect.Settings.Count);
                Assert.Equal("false.positive", cfgSect.Settings["section"].Value);
            }
            else
            {
                Assert.Equal(4, cfgSect.Settings.Count);
                Assert.Equal("false.static", cfgSect.Settings["section"].Value);
                Assert.Equal(behavior.ToString(), cfgSect.Settings["_recur_"].Value);
            }
        }

        [Theory]
        [InlineData(RecursionGuardValues.Throw, null)]
        [InlineData(RecursionGuardValues.Throw, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Stop, null)]
        [InlineData(RecursionGuardValues.Stop, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Allow, null)]
        [InlineData(RecursionGuardValues.Allow, "recursion-appexe.config")]
        public void Recursion_Buried(RecursionGuardValues behavior, string appConfig)
        {
            Environment.SetEnvironmentVariable("_recur_", behavior.ToString());
            var config = LoadMultiLevelConfig("recursion-machine.config", appConfig);

            // Direct
            try
            {
                var cfgSect = (AppSettingsSection)config.GetSection("buried.direct");
                Assert.False(behavior == RecursionGuardValues.Throw, "recur=Throw should not get to this point in buried.direct test.");
                Assert.NotNull(cfgSect);
                if (appConfig == null)
                {
                    Assert.Equal(5, cfgSect.Settings.Count);
                    Assert.Equal("was here", cfgSect.Settings["BuriedDirect"].Value);
                    Assert.Equal("was here", cfgSect.Settings["BuriedDirect:DirectRecur"].Value);
                    Assert.Equal("buried.direct", cfgSect.Settings["section"].Value);
                    Assert.Equal("direct.recur", cfgSect.Settings["BuriedDirect:section"].Value);
                    Assert.Equal("direct.recur", cfgSect.Settings["BuriedDirect:DirectRecur:section"].Value);
                }
                else
                {
                    Assert.Equal(5, cfgSect.Settings.Count);
                    Assert.Equal("was here", cfgSect.Settings["DirectRecur"].Value);
                    Assert.Equal("was here", cfgSect.Settings["BuriedDirect"].Value);
                    Assert.Equal("app.buried.direct", cfgSect.Settings["section"].Value);
                    Assert.Equal("static.recur", cfgSect.Settings["DirectRecur:section"].Value);
                    Assert.Equal("static.recur", cfgSect.Settings["BuriedDirect:section"].Value);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                Assert.False(behavior == RecursionGuardValues.Stop);

                if (behavior == RecursionGuardValues.Throw)
                {
                    var msg = $"The ConfigurationBuilder 'DirectRecur[Test.RecursiveBuilder]' has recursively re-entered processing of the 'direct.recur' section.";

                    // The main exception should be wrapped, and include our error
                    // message at the top for easy diagnosis
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e, msg);

                    // The exception should be an InvalidOperationException and include
                    // a message about recursion, the section, and the builder.
                    Assert.NotNull(unwrapped);
                    Assert.IsType<InvalidOperationException>(unwrapped);
                    Assert.Equal(msg, unwrapped.Message);
                }
                else if (behavior == RecursionGuardValues.Allow)
                {
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e);

                    // The inner exception should be a "Stack Overflow"
                    Assert.NotNull(unwrapped);
                    Assert.IsType<ConfigBuildersTestException>(unwrapped);
                    Assert.Equal("StackOverflow", unwrapped.Message);
                }
            }

            // Indirect Recursion
            try
            {
                var cfgSect = (AppSettingsSection)config.GetSection("buried.indirect");
                Assert.False(behavior == RecursionGuardValues.Throw, "recur=Throw should not get to this point in buried.indirect test.");
                Assert.NotNull(cfgSect);
                if (appConfig == null)
                {
                    Assert.Equal(7, cfgSect.Settings.Count);
                    Assert.Equal("was here", cfgSect.Settings["BuriedIndirect"].Value);
                    Assert.Equal("was here", cfgSect.Settings["BuriedIndirect:Indirect2"].Value);
                    Assert.Equal("was here", cfgSect.Settings["BuriedIndirect:Indirect2:Indirect1"].Value);
                    Assert.Equal("buried.indirect", cfgSect.Settings["section"].Value);
                    Assert.Equal("indirect.two", cfgSect.Settings["BuriedIndirect:section"].Value);
                    Assert.Equal("indirect.one", cfgSect.Settings["BuriedIndirect:Indirect2:section"].Value);
                    Assert.Equal("indirect.two", cfgSect.Settings["BuriedIndirect:Indirect2:Indirect1:section"].Value);
                }
                else
                {
                    Assert.Equal(3, cfgSect.Settings.Count);
                    Assert.Equal("was here", cfgSect.Settings["BuriedIndirect"].Value);
                    Assert.Equal("buried.indirect", cfgSect.Settings["section"].Value);
                    Assert.Equal("indirect.three", cfgSect.Settings["BuriedIndirect:section"].Value);
                }
            }
            catch (ConfigurationErrorsException e)
            {
                Assert.False(behavior == RecursionGuardValues.Stop);

                if (behavior == RecursionGuardValues.Throw)
                {
                    var msg = $"The ConfigurationBuilder 'Indirect2[Test.RecursiveBuilder]' has recursively re-entered processing of the 'indirect.two' section.";

                    // The main exception should be wrapped, and include our error
                    // message at the top for easy diagnosis
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e, msg);

                    // The exception should be an InvalidOperationException and include
                    // a message about recursion, the section, and the builder.
                    Assert.NotNull(unwrapped);
                    Assert.IsType<InvalidOperationException>(unwrapped);
                    Assert.Equal(msg, unwrapped.Message);
                }
                else if (behavior == RecursionGuardValues.Allow)
                {
                    Exception unwrapped = TestHelper.AssertExceptionIsWrapped(e);

                    // The inner exception should be a "Stack Overflow"
                    Assert.NotNull(unwrapped);
                    Assert.IsType<ConfigBuildersTestException>(unwrapped);
                    Assert.Equal("StackOverflow", unwrapped.Message);
                }
            }
        }

        [Theory]
        [InlineData(RecursionGuardValues.Throw, null)]
        [InlineData(RecursionGuardValues.Throw, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Stop, null)]
        [InlineData(RecursionGuardValues.Stop, "recursion-appexe.config")]
        [InlineData(RecursionGuardValues.Allow, null)]
        [InlineData(RecursionGuardValues.Allow, "recursion-appexe.config")]
        public void Recursion_Reentry(RecursionGuardValues behavior, string appConfig)
        {
            Environment.SetEnvironmentVariable("_recur_", behavior.ToString());
            var config = LoadMultiLevelConfig("recursion-machine.config", appConfig);
            string target = (appConfig == null) ? "reentry.target" : "app.reentry.target";


            //
            // Re-Entry (Not recursion) Allowed
            //
            var cfgSect = (AppSettingsSection)config.GetSection("reentry.start");
            Assert.NotNull(cfgSect);
            Assert.Equal(4, cfgSect.Settings.Count);
            Assert.Equal("was here", cfgSect.Settings["Reentry"].Value);
            Assert.Equal("stopped at target", cfgSect.Settings["Reentry:Reentry"].Value);
            Assert.Equal(target, cfgSect.Settings["Reentry:section"].Value);
            Assert.Equal("reentry.start", cfgSect.Settings["section"].Value);
        }

        // ======================================================================
        //   Helpers
        // ======================================================================
        Configuration LoadMultiLevelConfig(string machine, string appexe = null)
        {
            var filemap = new ExeConfigurationFileMap();

            var configFile = String.IsNullOrEmpty(machine) ? "empty.config" : machine;
            if (!System.IO.File.Exists(configFile) && !System.IO.Path.IsPathRooted(configFile))
                configFile = System.IO.Path.Combine("testConfigFiles", configFile);
            filemap.MachineConfigFilename = configFile;

            configFile = String.IsNullOrEmpty(appexe) ? "empty.config" : appexe;
            if (!System.IO.File.Exists(configFile) && !System.IO.Path.IsPathRooted(configFile))
                configFile = System.IO.Path.Combine("testConfigFiles", configFile);
            filemap.ExeConfigFilename = configFile;

            return ConfigurationManager.OpenMappedExeConfiguration(filemap, ConfigurationUserLevel.None);
        }
    }

    public class ConfigBuildersTestException : Exception
    {
        public ConfigBuildersTestException(string msg) : base(msg) { }
    }

    public class InstanceCheck : KeyValueConfigBuilder
    {
        Guid _guid;

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            _guid = Guid.NewGuid();
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            return new Dictionary<string, string>() { { Name, _guid.ToString() } };
        }

        public override string GetValue(string key)
        {
            if (key == "not-a-guid")
                return _guid.ToString();

            return null;
        }
    }

    public class RecursiveBuilder : KeyValueConfigBuilder
    {
        static int failSafe = 0;
        string section = null;
        protected ConfigurationSection currentSection = null;

        public override void Initialize(string name, NameValueCollection config)
        {
            config["recur"] = Environment.GetEnvironmentVariable("_recur_");
            base.Initialize(name, config);
            section = config["section"];
        }

        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            base.LazyInitialize(name, config);
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            try
            {
                if (++failSafe > 15)
                    throw new ConfigBuildersTestException("StackOverflow");

                currentSection = configSection;
                var cs = base.ProcessConfigurationSection(configSection);
                currentSection = null;
                return cs;
            }
            finally
            {
                failSafe--;
            }
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            // Special case: Do nothing if we are working on 'reentry.target' section.
            if (currentSection?.SectionInformation.Name == "reentry.target")
                return new Dictionary<string, string>() { { Name, "stopped at target" } };

            if (section != null)
            {
                var settings = new Dictionary<string, string>() { { Name, "was here" } };

                var configSection = currentSection.CurrentConfiguration.GetSection(section) as AppSettingsSection;

                if (configSection != null) foreach (var s in configSection.Settings.AllKeys)
                        settings.Add($"{Name}:{s}", configSection.Settings[s].Value);

                return settings;
            }

            return new Dictionary<string, string>() { { Name, "was here" } };
        }

        public override string GetValue(string key)
        {
            // We only do stuff in Greedy mode for this test
            return null;
        }
    }
}
