using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class CommonBuilderTests
    {
        public static string CommonKVPrefix = "CPFX-";
        public static string CommonKVExtraPrefix = "Prefix-";
        public static NameValueCollection CommonKeyValuePairs = new NameValueCollection() {
            { $"{CommonKVPrefix}TestKey", "TestValue1" },
            { $"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey", "testvalue2" },
            { $"{CommonKVPrefix}Value-Needs-Escaping", "Value \'with\" question@ble C#ar&ct*rs <in> it." }
        };

        static AppSettingsSection GetAppSettings()
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = (AppSettingsSection)cfg.AppSettings;
            appSettings.Settings.Add("TestKey", "justTestKeyOld");
            appSettings.Settings.Add($"{CommonKVPrefix}TestKey", "testKeyValueOld");
            appSettings.Settings.Add($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey", "prefixedValueOld");
            appSettings.Settings.Add($"{CommonKVExtraPrefix}TestKey", "justExtraValueOld");
            appSettings.Settings.Add("UnknownTestKey", "untouchedValue");
            return appSettings;
        }

        // ======================================================================
        //   GetValue
        //      - Gets what is there.
        //      - Does not get what is not there.
        //      - Is NOT case-sensitive.
        //      - Does not care about prefix or stripPrefix.
        //      - Does not do any character encoding/escaping.
        //      - Does not care about charMap.
        // ======================================================================

        public static void GetValue(Func<KeyValueConfigBuilder> builderFactory, string name, NameValueCollection settings = null, bool caseSensitive = false)
        {
            NameValueCollection customSettings, baseSettings = settings ?? new NameValueCollection();
            KeyValueConfigBuilder builder = TestHelper.CreateBuilder(builderFactory, name, baseSettings);

            // Gets what is there.
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));

            // Does not get what is not there.
            Assert.Null(builder.GetValue("This-Value-Does-Not-Exist"));

            // Is NOT case-sensitive.
            if (!caseSensitive)
                Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey".ToLower()));
            // Or maybe it is for some reason. (Looking at you Azure App Config.)
            else
                Assert.Null(builder.GetValue($"{CommonKVPrefix}TestKey".ToLower()));

            // Does not care about prefix...
            customSettings = new NameValueCollection(baseSettings);
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));

            // ...or stripPrefix...
            customSettings = new NameValueCollection(baseSettings);
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));

            // ...even if there is no prefix given.
            customSettings = new NameValueCollection(baseSettings);
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));

            // Does not escape values.
            customSettings = new NameValueCollection(baseSettings);
            customSettings["escapeExpandedValues"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));
            Assert.Equal(CommonKeyValuePairs["{CommonUniversalPrefix}Value-Needs-Escaping"], builder.GetValue("{CommonUniversalPrefix}Value-Needs-Escaping"));

            // Does not care about charMap.
            customSettings = new NameValueCollection(baseSettings);
            customSettings["charMap"] = "e=@";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}TestKey"));
            Assert.Null(builder.GetValue("T@stK@y"));
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], builder.GetValue($"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));
            Assert.Null(builder.GetValue("Pr@fix-T@stK@y"));
        }

        // ======================================================================
        //   GetAllValues
        //      - Has existing values.
        //      - Does not contain what is not there.
        // ======================================================================
        public static void GetAllValues(Func<KeyValueConfigBuilder> builderFactory, string name, NameValueCollection settings = null,
            Func<ICollection<KeyValuePair<string, string>>, string, string> getValueFromCollection = null)
        {
            NameValueCollection customSettings, baseSettings = settings ?? new NameValueCollection();
            KeyValueConfigBuilder builder = TestHelper.CreateBuilder(builderFactory, name, baseSettings);
            if (getValueFromCollection == null)
                getValueFromCollection = TestHelper.GetValueFromCollection;

            // Has all the test values.
            var allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], getValueFromCollection(allValues, key));

            // Does not contain what is not there.
            Assert.Null(getValueFromCollection(allValues, "This-Value-Does-Not-Exist"));

            // =============================================================================
            // Works with Prefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues($"{CommonKVPrefix}{CommonKVExtraPrefix}");

            // Has all the test values.
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], getValueFromCollection(allValues, $"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));

            // Does not contain what is not there.
            Assert.Null(getValueFromCollection(allValues, $"{CommonKVPrefix}TestKey"));

            // =========================================================================================
            // Works with Prefix... and Strip has no effect (KVCB base handles all stripping tasks.)
            customSettings = new NameValueCollection(baseSettings);
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues($"{CommonKVPrefix}{CommonKVExtraPrefix}");

            // Has all the test values.
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], getValueFromCollection(allValues, $"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"));

            // Does not contain what is not there.
            Assert.Null(getValueFromCollection(allValues, $"{CommonKVPrefix}TestKey"));

            // =========================================================================================
            // Does not escape values.
            customSettings = new NameValueCollection(baseSettings);
            customSettings["escapeExpandedValues"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], getValueFromCollection(allValues, key));

            // Does not care about charMap.
            customSettings = new NameValueCollection(baseSettings);
            customSettings["charMap"] = "e=@";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], getValueFromCollection(allValues, key));
            Assert.Null(getValueFromCollection(allValues, "T@stK@y"));
        }

        // ======================================================================
        //   "Full Stack" basics
        // ======================================================================
        public static void ProcessConfigurationSection(Func<KeyValueConfigBuilder> builderFactory, string name, NameValueCollection settings = null)
        {
            NameValueCollection customSettings, baseSettings = settings ?? new NameValueCollection();

            // =============================================================================
            // Strict Mode

            // Basics
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Strict.ToString();
            KeyValueConfigBuilder builder = TestHelper.CreateBuilder(builderFactory, name, baseSettings);
            var appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("justTestKeyOld", appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // Just the 5 existing appSettings. Nothing gets added in 'Strict' mode.

            // Works with Prefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Strict.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("justTestKeyOld", appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // Just the 5 existing appSettings. Nothing gets added in 'Strict' mode.

            // Works with Prefix and StripPrefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Strict.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal("prefixedValueOld", appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // Just the 5 existing appSettings. Nothing gets added in 'Strict' mode.

            // Variation on Prefix and StripPrefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Strict.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal("prefixedValueOld", appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // Just the 5 existing appSettings. Nothing gets added in 'Strict' mode.


            // =============================================================================
            // Greedy Mode

            // Basics
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Greedy.ToString();
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("justTestKeyOld", appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}Value-Needs-Escaping"], appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(6, appSettings.Settings.Count);    // 5 existing appSettings, +1 'Value-Needs-Escaping' setting gets added.

            // Works with Prefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Greedy.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("justTestKeyOld", appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // 5 existing appSettings, no other settings get added.

            // Works with Prefix and StripPrefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Greedy.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}{CommonKVExtraPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal("prefixedValueOld", appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal("justExtraValueOld", appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(5, appSettings.Settings.Count);    // 5 existing appSettings, no other settings get added.

            // Variation on Prefix and StripPrefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings["mode"] = KeyValueMode.Greedy.ToString();
            customSettings["prefix"] = $"{CommonKVPrefix}";
            customSettings["stripPrefix"] = "true";
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}TestKey"], appSettings.Settings["TestKey"]?.Value);
            Assert.Equal("untouchedValue", appSettings.Settings["UnknownTestKey"]?.Value);
            Assert.Equal("testKeyValueOld", appSettings.Settings[$"{CommonKVPrefix}TestKey"]?.Value);
            Assert.Equal("prefixedValueOld", appSettings.Settings[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}{CommonKVExtraPrefix}TestKey"], appSettings.Settings[$"{CommonKVExtraPrefix}TestKey"]?.Value);
            Assert.Null(appSettings.Settings[$"{CommonKVPrefix}Value-Needs-Escaping"]?.Value);
            Assert.Equal(CommonKeyValuePairs[$"{CommonKVPrefix}Value-Needs-Escaping"], appSettings.Settings[$"Value-Needs-Escaping"]?.Value);
            Assert.Null(appSettings.Settings["This-Value-Does-Not-Exist"]?.Value);
            Assert.Equal(6, appSettings.Settings.Count);    // 5 existing appSettings, 'Value-Needs-Escaping' gets added.
        }
    }
}
