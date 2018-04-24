using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test
{
    [TestClass]
    public class BaseTests
    {
        // ======================================================================
        //   Common Parameters
        // ======================================================================
        [TestMethod]
        public void BaseParameters_Mode()
        {
            // Default Strict
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            Assert.AreEqual(builder.Mode, KeyValueMode.Strict);

            // Strict
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Strict" } });
            Assert.AreEqual(builder.Mode, KeyValueMode.Strict);

            // Greedy
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" } });
            Assert.AreEqual(builder.Mode, KeyValueMode.Greedy);

            // Expand
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" } });
            Assert.AreEqual(builder.Mode, KeyValueMode.Expand);

            // Invalid
            builder = new FakeConfigBuilder();
            Assert.ThrowsException<ConfigurationErrorsException>(() => {
                builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "InvalidModeDoesNotExist" } });
            });

            // Case-insensitive value
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "grEEdy" } });
            Assert.AreEqual(builder.Mode, KeyValueMode.Greedy);

            // Case sensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "MODE", "Expand" } });
            Assert.AreEqual(builder.Mode, KeyValueMode.Expand);
        }

        [TestMethod]
        public void BaseParameters_Prefix()
        {
            // Default empty string. (Not null)
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            Assert.AreEqual(builder.KeyPrefix, "");

            // Prefix, case preserved
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "_This_is_my_PREFIX:" } });
            Assert.AreEqual(builder.KeyPrefix, "_This_is_my_PREFIX:");

            // Case sensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "PREfix", "$This_is_my_other_PREFIX#" } });
            Assert.AreEqual(builder.KeyPrefix, "$This_is_my_other_PREFIX#");
        }

        [TestMethod]
        public void BaseParameters_StripPrefix()
        {
            // Default false
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            Assert.AreEqual(builder.StripPrefix, false);

            // True
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "stripPrefix", "True" } });
            Assert.AreEqual(builder.StripPrefix, true);

            // fAlSe
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "stripPrefix", "fAlSe" } });
            Assert.AreEqual(builder.StripPrefix, false);

            // Works with 'prefix'
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "Test_" }, { "stripPrefix", "TRUE" } });
            Assert.AreEqual(builder.StripPrefix, true);

            // Can be set in Greedy mode
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "stripPrefix", "TRUE" } });
            Assert.AreEqual(builder.StripPrefix, true);

            // Can be set in Expand mode
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "stripPrefix", "TRUE" } });
            Assert.AreEqual(builder.StripPrefix, true);

            // Case sensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "STRIppreFIX", "true" } });
            Assert.AreEqual(builder.StripPrefix, true);
        }

        [TestMethod]
        public void BaseParameters_TokenPattern()
        {
            // Default string. (Not null)
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            Assert.AreEqual(builder.TokenPattern, @"\$\{(\w+)\}");

            // TokenPattern, case preserved
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "tokenPattern", @"%([^\s+\W*#$&_-])}%" } });
            Assert.AreEqual(builder.TokenPattern, @"%([^\s+\W*#$&_-])}%");

            // Case sensitive attribute name
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "TOKenpaTTerN", @"\[pattern\]" } });
            Assert.AreEqual(builder.TokenPattern, @"\[pattern\]");

            // Protected setter
            builder = new FakeConfigBuilder();
            builder.Initialize("test", null);
            builder.SetTokenPattern("TestPattern");
            Assert.AreEqual(builder.TokenPattern, @"TestPattern");
        }

        // ======================================================================
        //   Behaviors
        // ======================================================================
        [TestMethod]
        public void BaseBehavior_Strict()
        {
            // Strict - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            XmlNode xmlInput = GetNode(rawXmlInput);
            Assert.AreEqual(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "Prefix_" }, { "stripPrefix", "TRUE" } });
            xmlInput = GetNode(rawXmlInput);
            Assert.AreEqual(xmlInput, builder.ProcessRawXml(xmlInput));

            // Strict - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "Prefix_" }});
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Strict - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);
        }

        [TestMethod]
        public void BaseBehavior_Expand()
        {
            // Expand - ProcessConfigurationSection is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" } });
            AppSettingsSection origSettings = GetAppSettings();
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual(origSettings.Settings.Count, newSettings.Settings.Count);
            foreach (string key in origSettings.Settings.AllKeys)
                Assert.AreEqual(origSettings.Settings[key].Value, newSettings.Settings[key]?.Value);

            // Expand - ProcessConfigurationSection is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual(origSettings.Settings.Count, newSettings.Settings.Count);
            foreach (string key in origSettings.Settings.AllKeys)
                Assert.AreEqual(origSettings.Settings[key].Value, newSettings.Settings[key]?.Value);

            // Expand - ProcessRawXml
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" } });
            XmlNode xmlInput = GetNode(rawXmlInput);
            XmlNode xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "TestKey1Value"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "${TestKey1}"));

            // Expand - ProcessRawXml with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "prefix", "Prefix_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "prefix", "prEFiX_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "prefix", "prEFiX_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "Prefix_TestKey1Value"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("TestKey1Value", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "TestKey1Value"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("Prefix_TestKey1Value", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "${TestKey1}"));

            // Expand - ProcessRawXml with alternate tokenPattern
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.AreEqual("ThisWasAnAlternateTokenPattern", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.AreEqual("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml does not work with alternate tokenPattern with no capture group
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%[\w:]+%%" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.AreEqual("%%Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.AreEqual("%%Prefix_Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with alternate tokenPattern and prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.AreEqual("%%Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.AreEqual("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));

            // Expand - ProcessRawXml with alternate tokenPattern and strip prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Expand" }, { "tokenPattern", @"%%([\w:]+)%%" }, { "prefix", "Prefix_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            xmlOutput = builder.ProcessRawXml(xmlInput);
            Assert.AreEqual("appSettings", xmlOutput.Name);
            Assert.AreEqual("val1", GetValueFromXml(xmlOutput, "TestKey1"));
            Assert.AreEqual("${TestKey1}", GetValueFromXml(xmlOutput, "test1"));
            Assert.AreEqual("expandTestValue", GetValueFromXml(xmlOutput, "${TestKey1}"));
            Assert.AreEqual("PrefixTest1", GetValueFromXml(xmlOutput, "TestKey"));
            Assert.AreEqual("PrefixTest2", GetValueFromXml(xmlOutput, "Prefix_TestKey"));
            Assert.AreEqual("${Prefix_TestKey1}", GetValueFromXml(xmlOutput, "PreTest2"));
            Assert.AreEqual("ThisWasAnAltTokenPatternWithPrefix", GetValueFromXml(xmlOutput, "AltTokenTest"));
            Assert.AreEqual("%%Prefix_Alt:Token%%", GetValueFromXml(xmlOutput, "AltTokenTest2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "Prefix_TestKey1"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey2"));
            Assert.IsNull(GetValueFromXml(xmlOutput, "TestKey1Value"));
        }

        [TestMethod]
        public void BaseBehavior_Greedy()
        {
            // Greedy - ProcessRawXml is a noop
            var builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" } });
            XmlNode xmlInput = GetNode(rawXmlInput);
            Assert.AreEqual(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessRawXml is a noop, even with prefix stuff
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "prefix", "PreFIX_" }, { "stripPrefix", "true" } });
            xmlInput = GetNode(rawXmlInput);
            Assert.AreEqual(xmlInput, builder.ProcessRawXml(xmlInput));

            // Greedy - ProcessConfigurationSection
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.AreEqual("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "prefix", "Prefix_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix - NOT Case-Sensitive
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("val1", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with prefix and strip
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "prefix", "preFIX_" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("PrefixTest2", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.IsNull(newSettings.Settings["Prefix_TestKey1"]?.Value);
            Assert.IsNull(newSettings.Settings["TestKey2"]?.Value);

            // Greedy - ProcessConfigurationSection with strip with no prefix
            builder = new FakeConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection() { { "mode", "Greedy" }, { "stripPrefix", "true" } });
            newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.AreEqual("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
            Assert.AreEqual("${TestKey1}", newSettings.Settings["test1"]?.Value);
            Assert.AreEqual("expandTestValue", newSettings.Settings["${TestKey1}"]?.Value);
            Assert.AreEqual("PrefixTest1", newSettings.Settings["TestKey"]?.Value);
            Assert.AreEqual("Prefix_TestKeyValue", newSettings.Settings["Prefix_TestKey"]?.Value);
            Assert.AreEqual("${Prefix_TestKey1}", newSettings.Settings["PreTest2"]?.Value);
            Assert.AreEqual("TestKey2Value", newSettings.Settings["TestKey2"]?.Value);
            Assert.AreEqual("Prefix_TestKey1Value", newSettings.Settings["Prefix_TestKey1"]?.Value);
        }


        // ======================================================================
        //   Errors
        // ======================================================================
        [TestMethod]
        public void BaseErrors_ProcessRawXml()
        {
            var builder = new FakeConfigBuilder() { FailGetValues = true };

            try
            {
                builder.Initialize("Esteban", new NameValueCollection() { { "mode", "Expand" } });
                builder.ProcessRawXml(GetNode(rawXmlInput));

                // If we don't get an exception, that's bad
                Assert.Fail();
            }
            catch (Exception e)
            {
                // ProcessRawXml exception in Expand mode contains builder name
                Assert.IsTrue(e.Message.Contains("Esteban"));
                Assert.IsTrue(e.ToString().Contains("Unique Exception Message in GetValue"));
            }

            // In Strict or Greedy modes, ProcessRawXml is a noop
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Joe", new NameValueCollection());
            builder.ProcessRawXml(GetNode(rawXmlInput));
            Assert.IsTrue(true);    // I hate implicit success. ;)

            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Jose", new NameValueCollection() { { "mode", "Greedy" } });
            builder.ProcessRawXml(GetNode(rawXmlInput));
            Assert.IsTrue(true);    // I hate implicit success. ;)
        }

        [TestMethod]
        public void BaseErrors_ProcessConfigurationSection()
        {
            var builder = new FakeConfigBuilder() { FailGetValues = true };
            try
            {
                builder.Initialize("Stefano", new NameValueCollection() { { "mode", "Strict" } });
                builder.ProcessConfigurationSection(GetAppSettings());

                // If we don't get an exception, that's bad
                Assert.Fail();
            }
            catch (Exception e)
            {
                // ProcessConfigurationSection exception in Strict mode contains builder name
                Assert.IsTrue(e.Message.Contains("Stefano"));
                Assert.IsTrue(e.ToString().Contains("Unique Exception Message in GetValue"));
            }

            builder = new FakeConfigBuilder() { FailGetValues = true };
            try
            {
                builder.Initialize("Stepanya", new NameValueCollection() { { "mode", "Greedy" } });
                builder.ProcessConfigurationSection(GetAppSettings());

                // If we don't get an exception, that's bad
                Assert.Fail();
            }
            catch (Exception e)
            {
                // ProcessConfigurationSection exception in Greedy mode contains builder name
                Assert.IsTrue(e.Message.Contains("Stepanya"));
                Assert.IsTrue(e.ToString().Contains("Unique Exception Message in GetAllValues"));
            }

            // In Expand mode, ProcessConfigurationSection is a noop
            builder = new FakeConfigBuilder() { FailGetValues = true };
            builder.Initialize("Josef", new NameValueCollection() { { "mode", "Expand" } });
            builder.ProcessConfigurationSection(GetAppSettings());
            Assert.IsTrue(true);    // I hate implicit success. ;)
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
                { "Prefix_Alt:Token", "ThisWasAnAltTokenPatternWithPrefix" }
            };

        public bool FailInit = false;
        public bool FailGetValues = false;

        public bool StripPrefix
        {
            get
            {
                Type t = typeof(FakeConfigBuilder);
                FieldInfo fi = t.BaseType.GetField("_stripPrefix", BindingFlags.NonPublic | BindingFlags.Instance);
                return (bool)fi.GetValue(this);
            }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (FailInit)
                throw new Exception("Generic Exception Message");

            base.Initialize(name, config);
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
}
