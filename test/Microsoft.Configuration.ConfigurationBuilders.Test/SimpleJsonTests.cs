using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class SimpleJsonFixture : IDisposable
    {
        public string CommonJsonFileName { get; private set; }
        public string JsonTestFileName { get; private set; }
        public string JsonConflictFileName { get; private set; }

        public SimpleJsonFixture()
        {
            // Get a clean json settings file
            CommonJsonFileName = Path.Combine(Environment.CurrentDirectory, "SimpleJsonTest_" + Path.GetRandomFileName() + ".json");
            if (File.Exists(CommonJsonFileName))
                File.Delete(CommonJsonFileName);

            // Populate the json file with key/value pairs that are needed for common tests
            string rawJson = "";
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                rawJson += $"  \"{key}\": \"{CommonBuilderTests.CommonKeyValuePairs[key].Replace(@"\", @"\\").Replace("\"", "\\\"")}\",\r\n";
            File.WriteAllText(CommonJsonFileName, $"{{\r\n{rawJson}}}");

            // Also find our custom json test file
            JsonTestFileName = Path.Combine(Environment.CurrentDirectory, "testConfigFiles", "simpleJsonTest.json");
            JsonConflictFileName = Path.Combine(Environment.CurrentDirectory, "testConfigFiles", "simpleJsonConflict.json");
        }

        public void Dispose()
        {
            File.Delete(CommonJsonFileName);
        }
    }

    public class SimpleJsonTests : IClassFixture<SimpleJsonFixture>
    {
        private readonly SimpleJsonFixture _fixture;

        public SimpleJsonTests(SimpleJsonFixture fixture)
        {
            _fixture = fixture;
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [Theory]
        [InlineData(SimpleJsonConfigBuilderMode.Flat)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional)]
        public void SimpleJson_GetValue(SimpleJsonConfigBuilderMode jsonMode)
        {
            CommonBuilderTests.GetValue(() => new SimpleJsonConfigBuilder(), $"SimpleJson{jsonMode.ToString()}GetValue",
                new NameValueCollection() { { "jsonFile", _fixture.CommonJsonFileName }, { "jsonMode", jsonMode.ToString() } });
        }

        [Theory]
        [InlineData(SimpleJsonConfigBuilderMode.Flat)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional)]
        public void SimpleJson_GetAllValues(SimpleJsonConfigBuilderMode jsonMode)
        {
            CommonBuilderTests.GetAllValues(() => new SimpleJsonConfigBuilder(), $"SimpleJson{jsonMode.ToString()}GetAll",
                new NameValueCollection() { { "jsonFile", _fixture.CommonJsonFileName }, { "jsonMode", jsonMode.ToString() } });
        }

        [Theory]
        [InlineData(SimpleJsonConfigBuilderMode.Flat)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional)]
        public void SimpleJson_ProcessConfigurationSection(SimpleJsonConfigBuilderMode jsonMode)
        {
            CommonBuilderTests.ProcessConfigurationSection(() => new SimpleJsonConfigBuilder(), $"SimpleJson{jsonMode.ToString()}ProcessConfig",
                new NameValueCollection() { { "jsonFile", _fixture.CommonJsonFileName }, { "jsonMode", jsonMode.ToString() } });
        }

        // ======================================================================
        //   SimpleJson parameters
        // ======================================================================
        [Fact]
        public void SimpleJson_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonDefault",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName } });

            // JsonFile
            var mappedRoot = Utils.MapPath(_fixture.JsonTestFileName);
            Assert.Equal(_fixture.JsonTestFileName, mappedRoot);  // Doesn't really matter. But this should be the case in this test.
            Assert.Equal(mappedRoot, builder.JsonFile);

            // JsonMode
            Assert.Equal(SimpleJsonConfigBuilderMode.Flat, builder.JsonMode);

            // Enabled
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // CharacterMap
            Assert.Empty(builder.CharacterMap);
        }

        [Fact]
        public void SimpleJson_Settings()
        {
            // JsonFile and JsonMode attributes are case insensitive
            var builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings1",
                new NameValueCollection() { { "JsONfilE", _fixture.JsonTestFileName }, { "jSONmODe", "seCTiOnaL" } });
            var mappedPath = Utils.MapPath(_fixture.JsonTestFileName);
            Assert.Equal(_fixture.JsonTestFileName, mappedPath);    // Does not have to be true functionally speaking, but it should be true here.
            Assert.Equal(mappedPath, builder.JsonFile);
            Assert.Equal(SimpleJsonConfigBuilderMode.Sectional, builder.JsonMode);

            // Everything is flattened in flat mode
            builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings2",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "jsonMode", "Flat" } });
            var allValues = builder.GetAllValues("");
            Assert.Equal(55, allValues.Count);
            Assert.Equal("From Json Root", TestHelper.GetValueFromCollection(allValues, "rootString"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "rootBoolean"));
            Assert.Equal("42", TestHelper.GetValueFromCollection(allValues, "rootInteger"));
            Assert.Equal("one", TestHelper.GetValueFromCollection(allValues, "rootArrayOfStrings:0"));
            Assert.Equal("two", TestHelper.GetValueFromCollection(allValues, "rootArrayOfStrings:1"));
            Assert.Equal("three", TestHelper.GetValueFromCollection(allValues, "rootArrayOfStrings:2"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "rootArrayOfBooleans:0"));
            Assert.Equal("False", TestHelper.GetValueFromCollection(allValues, "rootArrayOfBooleans:1"));
            Assert.Equal("7", TestHelper.GetValueFromCollection(allValues, "rootArrayOfIntegers:0"));
            Assert.Equal("8", TestHelper.GetValueFromCollection(allValues, "rootArrayOfIntegers:1"));
            Assert.Equal("9", TestHelper.GetValueFromCollection(allValues, "rootArrayOfIntegers:2"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "rootMixedArray:0"));
            Assert.Equal("2", TestHelper.GetValueFromCollection(allValues, "rootMixedArray:1"));
            Assert.Equal("three", TestHelper.GetValueFromCollection(allValues, "rootMixedArray:2"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "rootEmptyArray"));
            Assert.Equal("", TestHelper.GetValueFromCollection(allValues, "rootNull"));
            Assert.Equal("From appSettings", TestHelper.GetValueFromCollection(allValues, "appSettings:asString"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "appSettings:asBoolean"));
            Assert.Equal("1977", TestHelper.GetValueFromCollection(allValues, "appSettings:asInteger"));
            Assert.Equal("four", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfStrings:0"));
            Assert.Equal("five", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfStrings:1"));
            Assert.Equal("six", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfStrings:2"));
            Assert.Equal("False", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfBooleans:0"));
            Assert.Equal("False", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfBooleans:1"));
            Assert.Equal("3", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfIntegers:0"));
            Assert.Equal("1", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfIntegers:1"));
            Assert.Equal("4", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfIntegers:2"));
            Assert.Equal("15", TestHelper.GetValueFromCollection(allValues, "appSettings:asArrayOfIntegers:3"));
            Assert.Equal("False", TestHelper.GetValueFromCollection(allValues, "appSettings:asMixedArray:0"));
            Assert.Equal("0", TestHelper.GetValueFromCollection(allValues, "appSettings:asMixedArray:1"));
            Assert.Equal("not true", TestHelper.GetValueFromCollection(allValues, "appSettings:asMixedArray:2"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "appSettings:asEmptyArray"));
            Assert.Equal("", TestHelper.GetValueFromCollection(allValues, "appSettings:asNull"));
            Assert.Equal("From complex part of appSettings", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxString"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxBoolean"));
            Assert.Equal("801", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxInteger"));
            Assert.Equal("foo", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfStrings:0"));
            Assert.Equal("bar", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfStrings:1"));
            Assert.Equal("baz", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfStrings:2"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfBooleans:0"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfBooleans:1"));
            Assert.Equal("35", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfIntegers:0"));
            Assert.Equal("5", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxArrayOfIntegers:1"));
            Assert.Equal("True", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxMixedArray:0"));
            Assert.Equal("1", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxMixedArray:1"));
            Assert.Equal("yes", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxMixedArray:2"));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxEmptyArray"));
            Assert.Equal("", TestHelper.GetValueFromCollection(allValues, "appSettings:asComplex:cpxNull"));
            Assert.Equal("Custom Setting from Json", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomString"));
            Assert.Equal("5", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomInteger"));
            Assert.Equal("1", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:0"));
            Assert.Equal("2", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:1"));
            Assert.Equal("3", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:2"));
            Assert.Equal("Complex Setting 1", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:setting1"));
            Assert.Equal("Complex Setting 2", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:setting2"));
            Assert.Equal("one", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:0"));
            Assert.Equal("two", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:1"));
            Assert.Equal("three", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:2"));

            // Again, but one at a time
            builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings3",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "jsonMode", "Flat" } });
            Assert.Equal(55, allValues.Count);
            Assert.Equal("From Json Root", builder.GetValue("rootString"));
            Assert.Equal("True", builder.GetValue("rootBoolean"));
            Assert.Equal("42", builder.GetValue("rootInteger"));
            Assert.Equal("one", builder.GetValue("rootArrayOfStrings:0"));
            Assert.Equal("two", builder.GetValue("rootArrayOfStrings:1"));
            Assert.Equal("three", builder.GetValue("rootArrayOfStrings:2"));
            Assert.Equal("True", builder.GetValue("rootArrayOfBooleans:0"));
            Assert.Equal("False", builder.GetValue("rootArrayOfBooleans:1"));
            Assert.Equal("7", builder.GetValue("rootArrayOfIntegers:0"));
            Assert.Equal("8", builder.GetValue("rootArrayOfIntegers:1"));
            Assert.Equal("9", builder.GetValue("rootArrayOfIntegers:2"));
            Assert.Equal("True", builder.GetValue("rootMixedArray:0"));
            Assert.Equal("2", builder.GetValue("rootMixedArray:1"));
            Assert.Equal("three", builder.GetValue("rootMixedArray:2"));
            Assert.Null(builder.GetValue("rootEmptyArray"));
            Assert.Equal("", builder.GetValue("rootNull"));
            Assert.Equal("From appSettings", builder.GetValue("appSettings:asString"));
            Assert.Equal("True", builder.GetValue("appSettings:asBoolean"));
            Assert.Equal("1977", builder.GetValue("appSettings:asInteger"));
            Assert.Equal("four", builder.GetValue("appSettings:asArrayOfStrings:0"));
            Assert.Equal("five", builder.GetValue("appSettings:asArrayOfStrings:1"));
            Assert.Equal("six", builder.GetValue("appSettings:asArrayOfStrings:2"));
            Assert.Equal("False", builder.GetValue("appSettings:asArrayOfBooleans:0"));
            Assert.Equal("False", builder.GetValue("appSettings:asArrayOfBooleans:1"));
            Assert.Equal("3", builder.GetValue("appSettings:asArrayOfIntegers:0"));
            Assert.Equal("1", builder.GetValue("appSettings:asArrayOfIntegers:1"));
            Assert.Equal("4", builder.GetValue("appSettings:asArrayOfIntegers:2"));
            Assert.Equal("15", builder.GetValue("appSettings:asArrayOfIntegers:3"));
            Assert.Equal("False", builder.GetValue("appSettings:asMixedArray:0"));
            Assert.Equal("0", builder.GetValue("appSettings:asMixedArray:1"));
            Assert.Equal("not true", builder.GetValue("appSettings:asMixedArray:2"));
            Assert.Null(builder.GetValue("appSettings:asEmptyArray"));
            Assert.Equal("", builder.GetValue("appSettings:asNull"));
            Assert.Equal("From complex part of appSettings", builder.GetValue("appSettings:asComplex:cpxString"));
            Assert.Equal("True", builder.GetValue("appSettings:asComplex:cpxBoolean"));
            Assert.Equal("801", builder.GetValue("appSettings:asComplex:cpxInteger"));
            Assert.Equal("foo", builder.GetValue("appSettings:asComplex:cpxArrayOfStrings:0"));
            Assert.Equal("bar", builder.GetValue("appSettings:asComplex:cpxArrayOfStrings:1"));
            Assert.Equal("baz", builder.GetValue("appSettings:asComplex:cpxArrayOfStrings:2"));
            Assert.Equal("True", builder.GetValue("appSettings:asComplex:cpxArrayOfBooleans:0"));
            Assert.Equal("True", builder.GetValue("appSettings:asComplex:cpxArrayOfBooleans:1"));
            Assert.Equal("35", builder.GetValue("appSettings:asComplex:cpxArrayOfIntegers:0"));
            Assert.Equal("5", builder.GetValue("appSettings:asComplex:cpxArrayOfIntegers:1"));
            Assert.Equal("True", builder.GetValue("appSettings:asComplex:cpxMixedArray:0"));
            Assert.Equal("1", builder.GetValue("appSettings:asComplex:cpxMixedArray:1"));
            Assert.Equal("yes", builder.GetValue("appSettings:asComplex:cpxMixedArray:2"));
            Assert.Null(builder.GetValue("appSettings:asComplex:cpxEmptyArray"));
            Assert.Equal("", builder.GetValue("appSettings:asComplex:cpxNull"));
            Assert.Equal("Custom Setting from Json", builder.GetValue("customAppSettings:jsonCustomString"));
            Assert.Equal("5", builder.GetValue("customAppSettings:jsonCustomInteger"));
            Assert.Equal("1", builder.GetValue("customAppSettings:jsonCustomArray:0"));
            Assert.Equal("2", builder.GetValue("customAppSettings:jsonCustomArray:1"));
            Assert.Equal("3", builder.GetValue("customAppSettings:jsonCustomArray:2"));
            Assert.Equal("Complex Setting 1", builder.GetValue("customAppSettings:jsonCustomComplex:setting1"));
            Assert.Equal("Complex Setting 2", builder.GetValue("customAppSettings:jsonCustomComplex:setting2"));
            Assert.Equal("one", builder.GetValue("customAppSettings:jsonCustomComplex:jsonArrayOfSettings:0"));
            Assert.Equal("two", builder.GetValue("customAppSettings:jsonCustomComplex:jsonArrayOfSettings:1"));
            Assert.Equal("three", builder.GetValue("customAppSettings:jsonCustomComplex:jsonArrayOfSettings:2"));

            // Get just the custom settings
            builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings4",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "jsonMode", "Flat" } });
            allValues = builder.GetAllValues("customAppSettings:");
            Assert.Equal(10, allValues.Count);
            Assert.Equal("Custom Setting from Json", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomString"));
            Assert.Equal("5", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomInteger"));
            Assert.Equal("1", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:0"));
            Assert.Equal("2", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:1"));
            Assert.Equal("3", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomArray:2"));
            Assert.Equal("Complex Setting 1", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:setting1"));
            Assert.Equal("Complex Setting 2", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:setting2"));
            Assert.Equal("one", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:0"));
            Assert.Equal("two", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:1"));
            Assert.Equal("three", TestHelper.GetValueFromCollection(allValues, "customAppSettings:jsonCustomComplex:jsonArrayOfSettings:2"));

            // We only get AppSettings section in sectional mode
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;
            builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings5",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "jsonMode", "Sectional" }, { "mode", "Greedy" } });
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);
            Assert.Equal(30, appSettings.Settings.Count);
            Assert.Equal("From appSettings", appSettings.Settings["asString"]?.Value);
            Assert.Equal("True", appSettings.Settings["asBoolean"]?.Value);
            Assert.Equal("1977", appSettings.Settings["asInteger"]?.Value);
            Assert.Equal("four", appSettings.Settings["asArrayOfStrings:0"]?.Value);
            Assert.Equal("five", appSettings.Settings["asArrayOfStrings:1"]?.Value);
            Assert.Equal("six", appSettings.Settings["asArrayOfStrings:2"]?.Value);
            Assert.Equal("False", appSettings.Settings["asArrayOfBooleans:0"]?.Value);
            Assert.Equal("False", appSettings.Settings["asArrayOfBooleans:1"]?.Value);
            Assert.Equal("3", appSettings.Settings["asArrayOfIntegers:0"]?.Value);
            Assert.Equal("1", appSettings.Settings["asArrayOfIntegers:1"]?.Value);
            Assert.Equal("4", appSettings.Settings["asArrayOfIntegers:2"]?.Value);
            Assert.Equal("15", appSettings.Settings["asArrayOfIntegers:3"]?.Value);
            Assert.Equal("False", appSettings.Settings["asMixedArray:0"]?.Value);
            Assert.Equal("0", appSettings.Settings["asMixedArray:1"]?.Value);
            Assert.Equal("not true", appSettings.Settings["asMixedArray:2"]?.Value);
            Assert.Equal("", appSettings.Settings["asNull"]?.Value);
            Assert.Equal("From complex part of appSettings", appSettings.Settings["asComplex:cpxString"]?.Value);
            Assert.Equal("True", appSettings.Settings["asComplex:cpxBoolean"]?.Value);
            Assert.Equal("801", appSettings.Settings["asComplex:cpxInteger"]?.Value);
            Assert.Equal("foo", appSettings.Settings["asComplex:cpxArrayOfStrings:0"]?.Value);
            Assert.Equal("bar", appSettings.Settings["asComplex:cpxArrayOfStrings:1"]?.Value);
            Assert.Equal("baz", appSettings.Settings["asComplex:cpxArrayOfStrings:2"]?.Value);
            Assert.Equal("True", appSettings.Settings["asComplex:cpxArrayOfBooleans:0"]?.Value);
            Assert.Equal("True", appSettings.Settings["asComplex:cpxArrayOfBooleans:1"]?.Value);
            Assert.Equal("35", appSettings.Settings["asComplex:cpxArrayOfIntegers:0"]?.Value);
            Assert.Equal("5", appSettings.Settings["asComplex:cpxArrayOfIntegers:1"]?.Value);
            Assert.Equal("True", appSettings.Settings["asComplex:cpxMixedArray:0"]?.Value);
            Assert.Equal("1", appSettings.Settings["asComplex:cpxMixedArray:1"]?.Value);
            Assert.Equal("yes", appSettings.Settings["asComplex:cpxMixedArray:2"]?.Value);
            Assert.Equal("", appSettings.Settings["asComplex:cpxNull"]?.Value);

            // We only get CustomAppSettings section in sectional mode
            var customSettings = (AppSettingsSection)cfg.GetSection("customAppSettings");
            builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonSettings6",
                new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "jsonMode", "Sectional" }, { "mode", "Greedy" } });
            customSettings = (AppSettingsSection)builder.ProcessConfigurationSection(customSettings);
            Assert.Equal(10, customSettings.Settings.Count);
            Assert.Equal("Custom Setting from Json", customSettings.Settings["jsonCustomString"]?.Value);
            Assert.Equal("5", customSettings.Settings["jsonCustomInteger"]?.Value);
            Assert.Equal("1", customSettings.Settings["jsonCustomArray:0"]?.Value);
            Assert.Equal("2", customSettings.Settings["jsonCustomArray:1"]?.Value);
            Assert.Equal("3", customSettings.Settings["jsonCustomArray:2"]?.Value);
            Assert.Equal("Complex Setting 1", customSettings.Settings["jsonCustomComplex:setting1"]?.Value);
            Assert.Equal("Complex Setting 2", customSettings.Settings["jsonCustomComplex:setting2"]?.Value);
            Assert.Equal("one", customSettings.Settings["jsonCustomComplex:jsonArrayOfSettings:0"]?.Value);
            Assert.Equal("two", customSettings.Settings["jsonCustomComplex:jsonArrayOfSettings:1"]?.Value);
            Assert.Equal("three", customSettings.Settings["jsonCustomComplex:jsonArrayOfSettings:2"]?.Value);
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        [Theory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void SimpleJson_ErrorsOptional(KeyValueEnabled enabled)
        {
            // No jsonFile - not an 'optional' error. The file can not exist, but not specifying one is an oversight that should be called out.
            var exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SimpleJsonErrors1");
            else
                Assert.Null(exception);

            // Can't find jsonFile
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonErrors2",
                    new NameValueCollection() { { "jsonFile", "path-does-not-exist.json" }, { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SimpleJsonErrors2");
            else
                Assert.Null(exception);

            // Invalid jsonMode - not an 'optional' error
            exception = Record.Exception(() =>
            {
                TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonErrors3",
                    new NameValueCollection() { { "jsonFile", _fixture.JsonTestFileName }, { "enabled", enabled.ToString() }, { "jsonMode", "invalidMode" } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SimpleJsonErrors3");
            else
                Assert.Null(exception);
        }

        [Theory]
        [InlineData(SimpleJsonConfigBuilderMode.Flat, KeyValueEnabled.Enabled)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional, KeyValueEnabled.Enabled)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional, KeyValueEnabled.Disabled)]
        [InlineData(SimpleJsonConfigBuilderMode.Flat, KeyValueEnabled.Optional)]
        [InlineData(SimpleJsonConfigBuilderMode.Sectional, KeyValueEnabled.Optional)]
        // Flat/Disabled is left out because it calls GetAllValues() directly when the builder
        // has not initialized. Results in null-ref. Normally, Get*Value*() methods are not
        // called directly. The config system always goes through Process*() methods.
        //[InlineData(SimpleJsonConfigBuilderMode.Flat, KeyValueEnabled.Disabled)]
        public void SimpleJson_ErrorsConflict(SimpleJsonConfigBuilderMode jsonMode, KeyValueEnabled enabled)
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var exception = Record.Exception(() =>
            {
                var builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonConflict1",
                    new NameValueCollection() { { "jsonFile", _fixture.JsonConflictFileName }, { "enabled", enabled.ToString() }, { "jsonMode", jsonMode.ToString() } });

                if (jsonMode == SimpleJsonConfigBuilderMode.Sectional)
                {
                    var appSettings = cfg.AppSettings;
                    appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);
                }
                else
                {
                    var allValues = builder.GetAllValues("appSettings:");
                }
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SimpleJsonConflict1");
            else
                Assert.Null(exception);

            exception = Record.Exception(() =>
            {
                var builder = TestHelper.CreateBuilder<SimpleJsonConfigBuilder>(() => new SimpleJsonConfigBuilder(), "SimpleJsonConflict2",
                    new NameValueCollection() { { "jsonFile", _fixture.JsonConflictFileName }, { "enabled", enabled.ToString() }, { "jsonMode", "Sectional" } });

                if (jsonMode == SimpleJsonConfigBuilderMode.Sectional)
                {
                    var customAppSettings = (AppSettingsSection)cfg.GetSection("customAppSettings");
                    customAppSettings = (AppSettingsSection)builder.ProcessConfigurationSection(customAppSettings);
                }
                else
                {
                    var allValues = builder.GetAllValues("customAppSettings:");
                }
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "SimpleJsonConflict2");
            else
                Assert.Null(exception);
        }
    }
}
