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
    public class BaseTests
    {
        // ======================================================================
        //   Common Parameters
        // ======================================================================
        [Fact]
        public void BaseParameters_Mode()
        {
            // Default Strict
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.Equal(KeyValueMode.Strict, builder.Mode);

            // Strict
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Strict" } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);

            // Greedy
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            Assert.Equal(KeyValueMode.Greedy, builder.Mode);

            // Invalid
            builder = new FakeConfigBuilder();
            var exception = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "mode", "InvalidModeDoesNotExist" } });
            });
            TestHelper.ValidateWrappedException<ArgumentException>(exception);

            // No longer valid
            builder = new FakeConfigBuilder();
            exception = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" } });
            });
            TestHelper.ValidateWrappedException<ArgumentException>(exception);

            // Case insensitive value
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "grEEdy" } });
            Assert.Equal(KeyValueMode.Greedy, builder.Mode);

            // Case insensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "MODE", "Greedy" } });
            Assert.Equal(KeyValueMode.Greedy, builder.Mode);
        }

        [Fact]
        public void BaseParameters_Prefix()
        {
            // Default empty string. (Not null)
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.Equal("", builder.KeyPrefix);

            // Prefix, case preserved
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "_This_is_my_PREFIX:" } });
            Assert.Equal("_This_is_my_PREFIX:", builder.KeyPrefix);

            // Case insensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "PREfix", "$This_is_my_other_PREFIX#" } });
            Assert.Equal("$This_is_my_other_PREFIX#", builder.KeyPrefix);
        }

        [Fact]
        public void BaseParameters_StripPrefix()
        {
            // Default false
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.False(builder.StripPrefix);

            // True
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "stripPrefix", "True" } });
            Assert.True(builder.StripPrefix);

            // fAlSe
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "stripPrefix", "fAlSe" } });
            Assert.False(builder.StripPrefix);

            // Works with 'prefix'
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Test_" }, { "stripPrefix", "TRUE" } });
            Assert.True(builder.StripPrefix);

            // Can be set in Greedy mode
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "stripPrefix", "TRUE" } });
            Assert.True(builder.StripPrefix);

            // Can be set in Expand mode
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "stripPrefix", "TRUE" } });
            Assert.True(builder.StripPrefix);

            // Case insensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "STRIppreFIX", "true" } });
            Assert.True(builder.StripPrefix);
        }

        [Fact]
        public void BaseParameters_TokenPattern()
        {
            // Default string. (Not null)
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.Equal(@"\$\{(\w[\w-_$@#+,.:~]*)\}", builder.TokenPattern);

            // TokenPattern, case preserved
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "tokenPattern", @"%([^\s+\W*#$&_-])}%" } });
            Assert.Equal(@"%([^\s+\W*#$&_-])}%", builder.TokenPattern);

            // Case insensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "TOKenpaTTerN", @"\[pattern\]" } });
            Assert.Equal(@"\[pattern\]", builder.TokenPattern);

            // Protected setter
            builder = new FakeConfigBuilder();
            builder.Initialize("test");
            builder.SetTokenPattern("TestPattern");
            Assert.Equal(@"TestPattern", builder.TokenPattern);
        }

        [Fact]
        public void BaseParameters_Enabled()
        {
            // Default Optional
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // Enabled
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Enabled" } });
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // Disabled
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Disabled" } });
            Assert.Equal(KeyValueEnabled.Disabled, builder.Enabled);

            // Optional
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Optional" } });
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // True
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "True" } });
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // False
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "False" } });
            Assert.Equal(KeyValueEnabled.Disabled, builder.Enabled);

            // Invalid
            builder = new FakeConfigBuilder();
            var exception = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "enabled", "InvalidValueHere" } });
            });
            TestHelper.ValidateWrappedException<ArgumentException>(exception);

            // Case insensitive atttribute name and value
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "ENableE", "oPtIOnIAl" } });
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // Disabled results in no-ops
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Disabled" } });
            XmlNode xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));
            Assert.Equal(KeyValueEnabled.Disabled, builder.Enabled);
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(TestHelper.CompareAppSettings(newSettings, TestHelper.GetAppSettings()));

            // Disabled does (mostly) prevent exceptional failure.
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Disabled" } });
            xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));
            Assert.Equal(KeyValueEnabled.Disabled, builder.Enabled);
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(TestHelper.CompareAppSettings(newSettings, TestHelper.GetAppSettings()));

            // Optional allows "controlled" failure without exception. What is "controlled" and allowed is
            // builder-dependent and that logic belongs to each builder. No point in testing the logic of
            // FakeConfigBuilder here. However...
            // Optional does not prevent exceptional failure.
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Optional" } });
            var ex = Record.Exception(() =>
            {
                xmlInput = TestHelper.GetAppSettingsXml();
                Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));
            });
            //TestHelper.ValidateExceptionForBuilder<ArgumentException>(ex);
            Assert.Null(ex);    // Now that ProcessRawXml is a no-op, this shouldn't throw in any mode.
            ex = Record.Exception(() =>
            {
                Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);
                newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
                Assert.True(TestHelper.CompareAppSettings(newSettings, TestHelper.GetAppSettings()));
            });
            Assert.NotNull(ex);
        }

        [Fact]
        public void BaseParameters_Optional()
        {
// Optional is obsolete, but IsOptional is not. And we still want to test that they all play together nicely.
#pragma warning disable CS0618 // Type or member is obsolete

            // Default true
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            Assert.True(builder.Optional);
            Assert.True(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // true
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "optional", "true" } });
            Assert.True(builder.Optional);
            Assert.True(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // false
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "optional", "false" } });
            Assert.False(builder.Optional);
            Assert.False(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // Case insensitive attribute name and value
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "opTiONAL", "fALsE" } });
            Assert.False(builder.Optional);
            Assert.False(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // Invalid value
            builder = new FakeConfigBuilder();
            var exception = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "opTiONAL", "NotAtAllValid" } });
            });
            TestHelper.ValidateWrappedException<FormatException>(exception);

            // IsOptional Property reflects Enabled (x3)
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Enabled" } });
            Assert.False(builder.Optional);
            Assert.False(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Disabled" } });
            Assert.True(builder.Optional);
            Assert.True(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Disabled, builder.Enabled);
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "enabled", "Optional" } });
            Assert.True(builder.Optional);
            Assert.True(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // Enabled takes precedence over 'optional'
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "optional", "false" }, { "enabled", "optional" } });
            Assert.True(builder.Optional);
            Assert.True(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // Optional not even consulted if 'enabled' is specified
            builder = new FakeConfigBuilder();
            var ex = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "optional", "would_throw" }, { "enabled", "enabled" } });
            });
            Assert.Null(ex);
            Assert.False(builder.Optional);
            Assert.False(builder.IsOptional);
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);
#pragma warning restore CS0618 // Type or member is obsolete
        }


        // ======================================================================
        //   Behaviors
        // ======================================================================
        [Fact]
        public void BaseBehavior_Strict()
        {
            // Strict - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            XmlNode xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Prefix_" }, { "stripPrefix", "TRUE" } });
            xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);

            // Strict - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
        }

        [Fact]
        public void BaseBehavior_Token()
        {
            // Token - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            XmlNode xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Token - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "PreFIX_" }, { "stripPrefix", "true" } });
            xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Token - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);

            // Token - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "prEFiX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "prEFiX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["Prefix_TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("ThisWasAnAlternateTokenPattern", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection does not work with alternate tokenPattern with no capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%[\w:]+%%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection does not blow up with alternate tokenPattern with empty capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%(.?)%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern and prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern and strip prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);
        }

        [Fact]
        public void BaseBehavior_Greedy()
        {
            // Greedy - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            XmlNode xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "PreFIX_" }, { "stripPrefix", "true" } });
            xmlInput = TestHelper.GetAppSettingsXml();
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
        }

        // ======================================================================
        //   Extension Points
        // ======================================================================
        [Fact]
        public void Ext_KeyMapping()
        {
            // Strict
            var builder = new FakeKeyMappingConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix#TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("MappingTest1", newSettings.Settings["Prefix_Alt_Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt_Token"]?.Value);
            Assert.Equal("ThisWasADifferentAlternateTokenPattern", newSettings.Settings["Alt:Token"]?.Value);

            // Strict with prefix
            builder = new FakeKeyMappingConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix#TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("MappingTest1", newSettings.Settings["Prefix_Alt_Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt_Token"]?.Value);
            Assert.Equal("MappingTest2", newSettings.Settings["Alt:Token"]?.Value);

            // Greedy
            builder = new FakeKeyMappingConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix#TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix#TestKey1"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("MappingTest1", newSettings.Settings["Prefix_Alt_Token"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["Prefix#Alt:Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_Alt:Token"]?.Value);
            Assert.Equal("ThisWasADifferentAlternateTokenPattern", newSettings.Settings["Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt_Token"]?.Value);
            Assert.Equal("ThisWasAnAlternateTokenPattern", newSettings.Settings["Alt:Token"]?.Value);

            // Greedy with prefix and stripping
            builder = new FakeKeyMappingConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "Prefix_" }, { "stripPrefix", "TRUE" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix#TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix#TestKey1"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("MappingTest1", newSettings.Settings["Prefix_Alt_Token"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["Alt:Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt_Token"]?.Value);

            // Greedy with interesting prefix
            builder = new FakeKeyMappingConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "Prefix:" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix:TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix#TestKey"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["Prefix:TestKey1"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix#TestKey1"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("MappingTest1", newSettings.Settings["Prefix_Alt_Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_Alt:Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix#Alt_Token"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["Prefix#Alt:Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix#Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix:Alt_Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix:Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Prefix:Alt:Token"]?.Value);
            Assert.Equal("MappingTest2", newSettings.Settings["Alt:Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt#Token"]?.Value);
            Assert.Null(newSettings.Settings["Alt_Token"]?.Value);
        }

        [Fact]
        public void Ext_EscapeExpandedValues()
        {
            // Escaping values is all on the value side. No need to check prefix shenanigans.
            // Also it should only apply in modes that expand tokens, ie, 'Token' mode.

            string rawString = "A & really ' b@d \" unescaped < string > from ` (somewhere) = can + really ? be * very ^ very # bad.";
            string escapedString = "A &amp; really &apos; b@d &quot; unescaped &lt; string &gt; from ` (somewhere) = can + really ? be * very ^ very # bad.";

            // Token escapes values
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "escapeExpandedValues", "true" } });
            var newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(builder.EscapeValues);
            Assert.Equal(escapedString, newSettings.Settings["EscapedStringFromToken"]?.Value);

            // Token does not escape value when not asked to
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "escapeExpandedValues", "false" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.False(builder.EscapeValues);
            Assert.Equal(rawString, newSettings.Settings["EscapedStringFromToken"]?.Value);

            // Token does not escape value by default
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.False(builder.EscapeValues);
            Assert.Equal(rawString, newSettings.Settings["EscapedStringFromToken"]?.Value);

            // Strict does not
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Strict" }, { "escapeExpandedValues", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(builder.EscapeValues);
            Assert.Equal(rawString, newSettings.Settings["StringToEscapeMaybe"]?.Value);

            // Greedy does not
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "escapeExpandedValues", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(builder.EscapeValues);
            Assert.Equal(rawString, newSettings.Settings["StringToEscapeMaybe"]?.Value);

            // Case insensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "ESCapeExpaNDEDValUes", "tRUe" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.True(builder.EscapeValues);
            Assert.Equal(escapedString, newSettings.Settings["EscapedStringFromToken"]?.Value);

            // Attribute value invalid
            builder = new FakeConfigBuilder();
            var exception = Record.Exception(() =>
            {
                builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "escapeExpandedValues", "not_true" } });
            });
            TestHelper.ValidateWrappedException<FormatException>(exception);
        }

        [Fact]
        public void Ext_CharMap()
        {
            Dictionary<string, string> sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "TestKey1", "Regular TestKey1Value" },
                { "T@xtK@y1", "TestKey1Value From Mapped Key" },
                { "TestKey2", "Regular TestKey2Value" },
                { "T@xtK@y2", "TestKey2Value From Mapped Key" },
                { "TI=s,KI=y1", "TestKey1Value From Escape-Mapped Key" },
                { "TekmtvtKey1", "TestKey1Value from charmap upon charmap" },
                { "TekyotKey1", "Bad Value. Charmap applied in sorted order." },
                { "T'>\"&<tK'>y1", "TestKey1Value with weird chars" },
                { "Pr@fixx_T@xtK@y1", "TestKey1Value From Prefixed And Mapped Key" },
            };

            // Fake Builder has no charMap by default
            var builder = new FakeConfigBuilder();
            builder.Initialize("test");
            Assert.NotNull(builder.CharacterMap);
            Assert.Empty(builder.CharacterMap);

            // Single character mapping
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "charMap", "a=b" } });
            Assert.NotNull(builder.CharacterMap);
            Assert.Single(builder.CharacterMap);
            Assert.Equal("b", builder.CharacterMap["a"]);

            // Multiple character mappings
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "charMap", "a=b,c=d,67=789" } });
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("b", builder.CharacterMap["a"]);
            Assert.Equal("d", builder.CharacterMap["c"]);
            Assert.Equal("789", builder.CharacterMap["67"]);

            // Character mapping with escapes
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "charMap", "a===,,b==,,,==c=d,67=7,,8,,9" } });
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal(",b=,", builder.CharacterMap["a="]);
            Assert.Equal("d", builder.CharacterMap["=c"]);
            Assert.Equal("7,8,9", builder.CharacterMap["67"]);

            // Case insensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "CHarmAP", "a=b" } });
            Assert.NotNull(builder.CharacterMap);
            Assert.Single(builder.CharacterMap);
            Assert.Equal("b", builder.CharacterMap["a"]);

            // Simple mapping in Strict (s=x,e=@,2=1)
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "charMap", "s=x,e=@,2=1" } });
            var newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal("TestKey1Value From Mapped Key", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value From Mapped Key", newSettings.Settings["TestKey2"]?.Value);

            // Simple mapping in Greedy (s=x,e=@,2=1) - no effect really
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "charMap", "s=x,e=@,2=1" } });
            var oldSettings = TestHelper.GetAppSettings();
            var oldCount = oldSettings.Settings.Count; // The 'newSettings' that returns is actually update oldSettings. So get this count first.
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(oldSettings);
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal(oldCount + 7, newSettings.Settings.Count); // 2 replaced, 7 added
            // Untouched
            Assert.Equal("PrefixTest1", newSettings.Settings["Prefixx_TestKey1"]?.Value);
            Assert.Equal("PrefixTestTheSecond", newSettings.Settings["Prefixx_TestKey2"]?.Value);
            Assert.Equal("This is an odd one", newSettings.Settings["T@kyotK@y1"]?.Value);
            // Replaced
            Assert.Equal("Regular TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("Regular TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            // Added
            Assert.Equal("TestKey1Value From Mapped Key", newSettings.Settings["T@xtK@y1"]?.Value);
            Assert.Equal("TestKey2Value From Mapped Key", newSettings.Settings["T@xtK@y2"]?.Value);
            Assert.Equal("TestKey1Value From Escape-Mapped Key", newSettings.Settings["TI=s,KI=y1"]?.Value);
            Assert.Equal("TestKey1Value from charmap upon charmap", newSettings.Settings["TekmtvtKey1"]?.Value);
            Assert.Equal("Bad Value. Charmap applied in sorted order.", newSettings.Settings["TekyotKey1"]?.Value);
            Assert.Equal("TestKey1Value with weird chars", newSettings.Settings["T'>\"&<tK'>y1"]?.Value);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["Pr@fixx_T@xtK@y1"]?.Value);
            Assert.Null(newSettings.Settings["T@stK@y2"]?.Value);

            // Escaped mapping in Token (t=,,,e=I==)
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "charMap", "t=,,,e=I==" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(2, builder.CharacterMap.Count);
            Assert.Equal("I=", builder.CharacterMap["e"]);
            Assert.Equal(",", builder.CharacterMap["t"]);
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value From Escape-Mapped Key", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value From Escape-Mapped Key"]?.Value);

            // With prefix in Strict
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["Prefixx_TestKey1"]?.Value);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["Prefixx_TestKey2"]?.Value);

            // With prefix in Greedy
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" } });
            oldSettings = TestHelper.GetAppSettings();
            oldCount = oldSettings.Settings.Count; // The 'newSettings' that returns is actually update oldSettings. So get this count first.
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(oldSettings);
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal(oldCount + 1, newSettings.Settings.Count); // None replaced, 1 added
            // Untouched
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["Prefixx_TestKey1"]?.Value);
            Assert.Equal("PrefixTestTheSecond", newSettings.Settings["Prefixx_TestKey2"]?.Value);
            // Replaced
            // Added
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["Pr@fixx_T@xtK@y1"]?.Value);
            Assert.Null(newSettings.Settings["Prefixx_T@xtK@y1"]?.Value);

            // With prefix in Token
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" } });
            oldSettings = TestHelper.GetAppSettings();
            oldCount = oldSettings.Settings.Count; // The 'newSettings' that returns is actually update oldSettings. So get this count first.
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(oldSettings);
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal(oldCount, newSettings.Settings.Count);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["CMPrefixTest1"]?.Value);
            Assert.Equal("CharmapPrefixTestValue1", newSettings.Settings["TestKey1Value From Prefixed And Mapped Key"]?.Value);

            // With prefix and strip in Strict
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["Prefixx_TestKey1"]?.Value);
            Assert.Equal("PrefixTestTheSecond", newSettings.Settings["Prefixx_TestKey2"]?.Value);

            // With prefix and strip in Greedy
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" }, { "stripPrefix", "true" } });
            oldSettings = TestHelper.GetAppSettings();
            oldCount = oldSettings.Settings.Count; // The 'newSettings' that returns is actually update oldSettings. So get this count first.
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(oldSettings);
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal(oldCount + 1, newSettings.Settings.Count); // None replaced, 1 added
            // Untouched
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("val2", newSettings.Settings["TestKey2"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["Prefixx_TestKey1"]?.Value);
            Assert.Equal("PrefixTestTheSecond", newSettings.Settings["Prefixx_TestKey2"]?.Value);
            // Replaced
            // Added
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["T@xtK@y1"]?.Value);
            Assert.Null(newSettings.Settings["Pr@fixx_T@xtK@y1"]?.Value);

            // With prefix and strip in Token
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "charMap", "s=x,e=@,2=1" }, { "prefix", "Prefixx_" }, { "stripPrefix", "true" } });
            oldSettings = TestHelper.GetAppSettings();
            oldCount = oldSettings.Settings.Count; // The 'newSettings' that returns is actually update oldSettings. So get this count first.
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(oldSettings);
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(3, builder.CharacterMap.Count);
            Assert.Equal("x", builder.CharacterMap["s"]);
            Assert.Equal("@", builder.CharacterMap["e"]);
            Assert.Equal("1", builder.CharacterMap["2"]);
            Assert.Equal(oldCount, newSettings.Settings.Count);
            Assert.Equal("TestKey1Value From Prefixed And Mapped Key", newSettings.Settings["test1"]?.Value);
            Assert.Equal("CharmapPrefixTestValue1", newSettings.Settings["${Prefixx_TestKey1}"]?.Value);
            Assert.Equal("${Prefixx_TestKey1}", newSettings.Settings["CMPrefixTest1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value From Prefixed And Mapped Key"]?.Value);

            // Mappings build on each other (k=x,x=*). Order matters?
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "charMap", "s=xyz,x=k,z=o,yo=mtv" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(4, builder.CharacterMap.Count);
            Assert.Equal("xyz", builder.CharacterMap["s"]);
            Assert.Equal("k", builder.CharacterMap["x"]);
            Assert.Equal("o", builder.CharacterMap["z"]);
            Assert.Equal("mtv", builder.CharacterMap["yo"]);
            Assert.Equal("TestKey1Value from charmap upon charmap", newSettings.Settings["TestKey1"]?.Value);

            // Does not apply to values/Does not interfere with escapeExpandedValues
            builder = new FakeConfigBuilder(sourceValues);
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "charMap", "s=\"&<,e='>" }, { "escapeExpandedValues", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            Assert.NotNull(builder.CharacterMap);
            Assert.Equal(2, builder.CharacterMap.Count);
            Assert.Equal("\"&<", builder.CharacterMap["s"]);
            Assert.Equal("'>", builder.CharacterMap["e"]);
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value with weird chars", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value with weird chars"]?.Value);
        }

        // ======================================================================
        //   UpdateConfigSettingWithAppSettings
        // ======================================================================
        // TODO: Test this method. Although I believe this does get a workout
        //      in the recursion tests.


        // ======================================================================
        //   Errors
        // ======================================================================
        [Fact]
        public void BaseErrors_ProcessConfigurationSection()
        {
            var builder = new FakeConfigBuilder() { FailGetValues = true };
            try
            {
                builder.Initialize("Stefano", new NameValueCollection() { { "mode", "Strict" } });
                builder.ProcessConfigurationSection(TestHelper.GetAppSettings());

                // If we don't get an exception, that's bad
                Assert.True(false);
            }
            catch (Exception e)
            {
                // ProcessConfigurationSection exception in Strict mode contains builder name
                Assert.Contains("Stefano", e.Message);
                Assert.Contains("Unique Exception Message in GetValue", e.ToString());
            }

            builder = new FakeConfigBuilder() { FailGetValues = true };
            try
            {
                builder.Initialize("Stepanya", new NameValueCollection() { { "mode", "Greedy" } });
                builder.ProcessConfigurationSection(TestHelper.GetAppSettings());

                // If we don't get an exception, that's bad
                Assert.True(false);
            }
            catch (Exception e)
            {
                // ProcessConfigurationSection exception in Greedy mode contains builder name
                Assert.Contains("Stepanya", e.Message);
                Assert.Contains("Unique Exception Message in GetAllValues", e.ToString());
            }
        }
    }

    // ======================================================================
    // Test Config Builders
    // ======================================================================
    class FakeConfigBuilder : KeyValueConfigBuilder
    {
        private readonly Dictionary<string, string> sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "TestKey1", "TestKey1Value" },
                { "TestKey2", "TestKey2Value" },
                { "Prefix_TestKey", "Prefix_TestKeyValue" },
                { "Prefix_TestKey1", "Prefix_TestKey1Value" },
                { "Alt:Token", "ThisWasAnAlternateTokenPattern" },
                { "Alt_Token", "ThisWasADifferentAlternateTokenPattern" },
                { "Prefix_Alt:Token", "ThisWasAnAltTokenPatternWithPrefix" },
                { "StringToEscapeMaybe", "A & really ' b@d \" unescaped < string > from ` (somewhere) = can + really ? be * very ^ very # bad." },
            };

        public bool FailGetValues = false;
        public bool FailInit = false;
        public bool ForceLazyInit = true;

        public FakeConfigBuilder() { }
        public FakeConfigBuilder(Dictionary<string, string> configValues)
        {
            sourceValues = configValues;
        }

        public bool StripPrefix
        {
            get
            {
                Type t = typeof(FakeConfigBuilder);
                PropertyInfo pi = t.BaseType.GetProperty("StripPrefix", BindingFlags.NonPublic | BindingFlags.Instance);
                return (bool)pi.GetValue(this);
            }
        }

        public override void Initialize(string name, NameValueCollection config = null)
        {
            if (FailInit)
                throw new Exception("Generic Exception Message");

            if (config == null)
                config = new NameValueCollection();

            base.Initialize(name, config);

            if (ForceLazyInit)
            {
                // Calling directly avoids some exception logic. Use 'EnsureInitialized' instead.
                //LazyInitialize(name, config);
                TestHelper.CallEnsureInitialized(this);
            }
        }

        public override string GetValue(string key)
        {
            if (FailGetValues)
                throw new Exception("Unique Exception Message in GetValue");

            return sourceValues.TryGetValue(key, out string value) ? value : null;
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            if (FailGetValues)
                throw new Exception("Unique Exception Message in GetAllValues");

            return sourceValues.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void SetTokenPattern(string newPattern)
        {
            this.TokenPattern = newPattern;
        }
    }

    class FakeKeyMappingConfigBuilder : FakeConfigBuilder
    {
        public override string MapKey(string key)
        {
            return key.Replace(":", "_");
        }

        public override string UpdateKey(string rawKey)
        {
            return rawKey.Replace("_", "#");
        }
    }
}
