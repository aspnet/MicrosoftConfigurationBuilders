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
            var exception = Record.Exception(() => {
                builder.Initialize("test", new NameValueCollection() { { "mode", "InvalidModeDoesNotExist" } });
                Assert.Equal(KeyValueMode.Strict, builder.Mode); // Will throw trying to read the mode
            });
            Assert.NotNull(exception);
            Assert.NotNull(exception.InnerException);
            Assert.IsType<ArgumentException>(exception.InnerException);

            // No longer valid
            builder = new FakeConfigBuilder();
            exception = Record.Exception(() => {
                builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" } });
                Assert.Equal(KeyValueMode.Strict, builder.Mode); // Will throw trying to read the mode
            });
            Assert.NotNull(exception);
            Assert.NotNull(exception.InnerException);
            Assert.IsType<ArgumentException>(exception.InnerException);

            // Case-insensitive value
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "grEEdy" } });
            Assert.Equal(KeyValueMode.Greedy, builder.Mode);

            // Case sensitive attribute name
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

            // Case sensitive attribute name
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

            // Case sensitive attribute name
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

            // Case sensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "TOKenpaTTerN", @"\[pattern\]" } });
            Assert.Equal(@"\[pattern\]", builder.TokenPattern);

            // Protected setter
            builder = new FakeConfigBuilder();
            builder.Initialize("test");
            builder.SetTokenPattern("TestPattern");
            Assert.Equal(@"TestPattern", builder.TokenPattern);
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
            XmlNode xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Prefix_" }, { "stripPrefix", "TRUE" } });
            xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection());
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "Prefix_" }});
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
        }

        [Fact]
        public void BaseBehavior_Token()
        {
            // Token - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            XmlNode xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Token - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "PreFIX_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Token - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);

            // Token - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "prEFiX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "prefix", "prEFiX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["Prefix_TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("TestKey1Value", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["TestKey1Value"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["${TestKey1}"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("ThisWasAnAlternateTokenPattern", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection does not work with alternate tokenPattern with no capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%[\w:]+%%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection does not blow up with alternate tokenPattern with empty capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%(.?)%" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern and prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("%%Alt:Token%%", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);

            // Token - ProcessConfigurationSection with alternate tokenPattern and strip prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", newSettings.Settings["AltTokenTest"]?.Value);
            Assert.Equal("%%Prefix_Alt:Token%%", newSettings.Settings["AltTokenTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
            Assert.Null(newSettings.Settings["TestKey1Value"]?.Value);
        }

        [Fact]
        public void BaseBehavior_Greedy()
        {
            // Greedy - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            XmlNode xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "PreFIX_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            Assert.Equal(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
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
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Equal("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.Equal("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.Equal("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.Equal("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.Equal("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.Null(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Greedy" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
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
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
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
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
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
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
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
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
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
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.Null(newSettings.Settings["TestKey2"]?.Value);
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
                builder.ProcessConfigurationSection(GetAppSettings());

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
                builder.ProcessConfigurationSection(GetAppSettings());

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


        // ======================================================================
        //   Helpers
        // ======================================================================
        const string rawXmlInput = @"
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

        XmlNode GetNode(string xmlInput)
        {
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(xmlInput);
            return doc.DocumentElement;
        }

        AppSettingsSection GetAppSettings()
        {
            AppSettingsSection appSettings = new AppSettingsSection();
            appSettings.Settings.Add("TestKey1", "val1");
            appSettings.Settings.Add("test1", "${TestKey1}");
            appSettings.Settings.Add("${TestKey1}", "expandTestValue");
            appSettings.Settings.Add("TestKey", "PrefixTest1");
            appSettings.Settings.Add("Prefix_TestKey", "PrefixTest2");
            appSettings.Settings.Add("PreTest2", "${Prefix_TestKey1}");
            appSettings.Settings.Add("Prefix_Alt_Token", "MappingTest1");
            appSettings.Settings.Add("Alt:Token", "MappingTest2");
            appSettings.Settings.Add("AltTokenTest", "%%Alt:Token%%");
            appSettings.Settings.Add("AltTokenTest2", "%%Prefix_Alt:Token%%");
            return appSettings;
        }

        string GetValueFromXml(XmlNode node, string key)
        {
            foreach (XmlNode child in node.SelectNodes("add"))
            {
                if (String.Compare(child.Attributes["key"].Value, key, true) == 0)
                    return child.Attributes["value"].Value;
            }

            return null;
        }
    }

    class FakeConfigBuilder : KeyValueConfigBuilder
    {
        static Dictionary<string, string> sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "TestKey1", "TestKey1Value" },
                { "TestKey2", "TestKey2Value" },
                { "Prefix_TestKey", "Prefix_TestKeyValue" },
                { "Prefix_TestKey1", "Prefix_TestKey1Value" },
                { "Alt:Token", "ThisWasAnAlternateTokenPattern" },
                { "Alt_Token", "ThisWasADifferentAlternateTokenPattern" },
                { "Prefix_Alt:Token", "ThisWasAnAltTokenPatternWithPrefix" }
            };

        public bool FailInit = false;
        public bool ForceLazyInit = true;
        public bool FailGetValues = false;

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
                LazyInitialize(name, config);
        }

        public override string GetValue(string key)
        {
            if (FailGetValues)
                throw new Exception("Unique Exception Message in GetValue");

            string value = null;
            return sourceValues.TryGetValue(key, out value) ? value : null;
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
