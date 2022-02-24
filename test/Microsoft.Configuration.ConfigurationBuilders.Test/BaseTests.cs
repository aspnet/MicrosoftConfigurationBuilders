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

            // Expand
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" } });
            Assert.Equal(KeyValueMode.RawToken, builder.Mode);

            // RawToken
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "RawToken" } });
            Assert.Equal(KeyValueMode.RawToken, builder.Mode);

            // Invalid
            builder = new FakeConfigBuilder();
            Assert.Throws<ArgumentException>(() => {
                builder.Initialize("test", new NameValueCollection() { { "mode", "InvalidModeDoesNotExist" } });
            });

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
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "stripPrefix", "TRUE" } });
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
        public void BaseBehavior_Expand()
        {
            // Expand - ProcessConfigurationSection is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" } });
            AppSettingsSection origSettings = GetAppSettings();
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(origSettings.Settings.Count, newSettings.Settings.Count);
            foreach (string key in origSettings.Settings.AllKeys)
                Assert.Equal(origSettings.Settings[key].Value, newSettings.Settings[key]?.Value);

            // Expand - ProcessConfigurationSection is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal(origSettings.Settings.Count, newSettings.Settings.Count);
            foreach (string key in origSettings.Settings.AllKeys)
                Assert.Equal(origSettings.Settings[key].Value, newSettings.Settings[key]?.Value);

            // Expand - ProcessRawXml
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" } });
            XmlNode xmlInput = GetNode(rawXmlInput);
            XmlNode xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "TestKey1Value"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "${TestKey1}"));

            // Expand - ProcessRawXml with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "prefix", "Prefix_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "prefix", "prEFiX_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "prefix", "prEFiX_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "Prefix_TestKey1Value"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "TestKey1Value"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "${TestKey1}"));

            // Expand - ProcessRawXml with alternate tokenPattern
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Equal("ThisWasAnAlternateTokenPattern", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml does not work with alternate tokenPattern with no capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%[\w:]+%%" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Equal("%%Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.Equal("%%Prefix_Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml does not blow up with alternate tokenPattern with empty capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%(.?)%" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Equal("%%Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.Equal("%%Prefix_Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with alternate tokenPattern and prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Equal("%%Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with alternate tokenPattern and strip prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.Equal("appSettings", xmlOutput.Name);
            Assert.Equal("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.Equal("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.Equal("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.Equal("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.Equal("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.Equal("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.Equal("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.Equal("%%Prefix_Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.Null(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.Null(GetValueFromXml(xmlOutput, "TestKey1Value"));
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
        public void BaseErrors_ProcessRawXml()
        {
            var builder = new FakeConfigBuilder() { FailGetValues = true };

            try
            {
                builder.Initialize("Esteban", new NameValueCollection() { { "mode", "Expand" } });
                builder.ProcessRawXml(GetNode(rawXmlInput));

                // If we don't get an exception, that's bad
                Assert.True(false);
            }
            catch (Exception e)
            {
                // ProcessRawXml exception in Expand mode contains builder name
                Assert.Contains("Esteban", e.Message);
                Assert.Contains("Unique Exception Message in GetValue", e.ToString());
            }

            // In Strict or Greedy modes, ProcessRawXml is a noop
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Joe", new NameValueCollection());
            builder.ProcessRawXml(GetNode(rawXmlInput));
            Assert.True(true);    // I hate implicit success. ;)

            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Jose", new NameValueCollection() { { "mode", "Greedy" } });
            builder.ProcessRawXml(GetNode(rawXmlInput));
            Assert.True(true);    // I hate implicit success. ;)
        }

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

            // In Expand mode, ProcessConfigurationSection is a noop
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Josef", new NameValueCollection() { { "mode", "Expand" } });
            builder.ProcessConfigurationSection(GetAppSettings());
            Assert.True(true);    // I hate implicit success. ;)
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
