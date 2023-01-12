using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class TestHelper
    {
        static readonly Type kvWrapperType;
        static readonly Type kvExceptionType;
        static readonly MethodInfo ensureInitialized;

        static TestHelper()
        {
            kvWrapperType = typeof(KeyValueConfigBuilder).Assembly.GetType("Microsoft.Configuration.ConfigurationBuilders.KeyValueConfigurationErrorsException");
            kvExceptionType = typeof(KeyValueConfigBuilder).Assembly.GetType("Microsoft.Configuration.ConfigurationBuilders.KeyValueConfigBuilderException");
            ensureInitialized = typeof(KeyValueConfigBuilder).GetMethod("EnsureInitialized", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        public static T CreateBuilder<T>(Func<T> builderFactory, string name, NameValueCollection attrs = null) where T : KeyValueConfigBuilder
        {
            T builder = builderFactory();
            NameValueCollection settings = attrs ?? new NameValueCollection();
            builder.Initialize(name, settings);
            CallEnsureInitialized(builder);
            return builder;
        }

        public static void CallEnsureInitialized(KeyValueConfigBuilder builder)
        {
            try
            {
                ensureInitialized.Invoke(builder, null);
            }
            catch (TargetInvocationException e)
            {
                if (e.InnerException != null)
                    throw e.InnerException;
                throw;
            }
        }

        public static string GetValueFromCollection(ICollection<KeyValuePair<string, string>> collection, string key)
        {
            foreach (var kvp in collection)
            {
                if (kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

        public static Exception ValidateFullyWrappedException(Exception e, string msg = null)
        {
            Assert.NotNull(e);
            if (msg != null)
                Assert.Contains(msg, e.Message);
            Assert.IsType(kvWrapperType, e);
            Assert.NotNull(e.InnerException);
            Assert.IsType(kvExceptionType, e.InnerException);
            Assert.NotNull(e.InnerException.InnerException);

            return e.InnerException.InnerException;
        }

        public static void ValidateWrappedException(Exception e, KeyValueConfigBuilder builder = null, Type exceptionType = null, params string[] msgs)
        {
            Assert.NotNull(e);
            Assert.NotNull(e.InnerException);

            if (builder != null)
                Assert.Contains(builder.Name, e.Message);

            if (exceptionType != null)
                Assert.IsType(exceptionType, e.InnerException);

            foreach (string msg in msgs)
                Assert.Contains(msg, e.Message);
        }

        public static void ValidateWrappedException<T>(Exception e, params string[] msgs) where T : Exception
        {
            ValidateWrappedException(e, null, typeof(T), msgs);
        }

        public static void ValidateWrappedException<T>(Exception e, KeyValueConfigBuilder builder = null, params string[] msgs) where T : Exception
        {
            ValidateWrappedException(e, builder, typeof(T), msgs);
        }

        public static void ValidateBasicException(Exception e, string msg = null, Type exceptionType = null)
        {
            Assert.NotNull(e);

            if (msg != null)
                Assert.Contains(msg, e.Message);

            if (exceptionType != null)
                Assert.IsType(exceptionType, e);
        }

        public static void ValidateBasicException<T>(Exception e, string msg = null) where T : Exception
        {
            ValidateBasicException(e, msg, typeof(T));
        }

        static readonly string rawXmlInput = @"
                <appSettings>
                    <add key=""TestKey1"" value=""val1"" />
                    <add key=""test1"" value=""${TestKey1}"" />
                    <add key=""${TestKey1}"" value=""expandTestValue"" />
                    <add key=""TestKey"" value=""PrefixTest1"" />
                    <add key=""Prefix_TestKey"" value=""PrefixTest2"" />
                    <add key=""PreTest2"" value=""${Prefix_TestKey1}"" />
                    <add key=""AltTokenTest"" value=""%%Alt:Token%%"" />
                    <add key=""AltTokenTest2"" value=""%%Prefix_Alt:Token%%"" />
                </appSettings>";

        public static XmlNode GetAppSettingsXml(string xmlInput = null)
        {
            xmlInput = xmlInput ?? rawXmlInput;
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xmlInput);
            return doc.DocumentElement;
        }

        public static AppSettingsSection GetAppSettings()
        {
            AppSettingsSection appSettings = new AppSettingsSection();
            appSettings.Settings.Add("TestKey1", "val1");
            appSettings.Settings.Add("TestKey2", "val2");
            appSettings.Settings.Add("test1", "${TestKey1}");
            appSettings.Settings.Add("${TestKey1}", "expandTestValue");
            appSettings.Settings.Add("TestKey", "PrefixTest1");
            appSettings.Settings.Add("Prefix_TestKey", "PrefixTest2");
            appSettings.Settings.Add("Prefixx_TestKey1", "PrefixTest1");
            appSettings.Settings.Add("Prefixx_TestKey2", "PrefixTestTheSecond");
            appSettings.Settings.Add("PreTest2", "${Prefix_TestKey1}");
            appSettings.Settings.Add("CMPrefixTest1", "${Prefixx_TestKey1}");
            appSettings.Settings.Add("${Prefixx_TestKey1}", "CharmapPrefixTestValue1");
            appSettings.Settings.Add("Prefix_Alt_Token", "MappingTest1");
            appSettings.Settings.Add("Alt:Token", "MappingTest2");
            appSettings.Settings.Add("AltTokenTest", "%%Alt:Token%%");
            appSettings.Settings.Add("AltTokenTest2", "%%Prefix_Alt:Token%%");
            appSettings.Settings.Add("StringToEscapeMaybe", "NothingScaryHere");
            appSettings.Settings.Add("EscapedStringFromToken", "${StringToEscapeMaybe}");
            appSettings.Settings.Add("T@kyotK@y1", "This is an odd one");

            return appSettings;
        }

        public static bool CompareAppSettings(AppSettingsSection as1, AppSettingsSection as2)
        {
            // The same object, or both null
            if (as1 == as2)
                return true;

            // Can't both be null here. So if either one is, the other is not.
            if (as1 == null || as2 == null)
                return false;

            if (as1.Settings.Count != as2.Settings.Count)
                return false;

            foreach (KeyValueConfigurationElement setting in as1.Settings)
            {
                if (as2.Settings[setting.Key].Value != setting.Value)
                    return false;
            }

            // No discrepancies
            return true;
        }

        public static Configuration LoadMultiLevelConfig(string machine, string appexe = null)
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

        public static Configuration LoadConfigFromString(ref string config)
        {
            string ignore = null;
            return LoadConfigFromString(ref config, ref ignore);
        }

        public static Configuration LoadConfigFromString(ref string machineConfig, ref string appConfig)
        {
            string mcFileName = null;
            string acFileName = null;

            if (!String.IsNullOrEmpty(machineConfig))
            {
                mcFileName = Path.GetTempFileName();
                FileInfo mcfi = new FileInfo(mcFileName);
                mcfi.Attributes = FileAttributes.Temporary;
                File.WriteAllText(mcFileName, machineConfig);
            }
            machineConfig = mcFileName;

            if (!String.IsNullOrEmpty(appConfig))
            {
                acFileName = Path.GetTempFileName();
                FileInfo acfi = new FileInfo(acFileName);
                acfi.Attributes = FileAttributes.Temporary;
                File.WriteAllText(acFileName, appConfig);
            }
            appConfig = acFileName;

            return LoadMultiLevelConfig(mcFileName, acFileName);
        }
    }
}
