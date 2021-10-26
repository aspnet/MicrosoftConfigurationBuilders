using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class ThreadSafeTests
    {

        [Fact]
        public void BaseErrors_ProcessConfigurationSection()
        {
            var builder = new LazyConfigBuilder();
            builder.Initialize("Stefano", new NameValueCollection() { { "mode", "Strict" } });
            AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
            Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);

        }

        [Fact]
        public async void TestThreadingAsync()
        {

            // Strict - ProcessRawXml is a noop, even with prefix stuff
            var builder = new LazyConfigBuilder();
            builder.Initialize("test", new System.Collections.Specialized.NameValueCollection());

            Exception ex = null;
            var count = 50;
            var random = new Random();
            var threads = Enumerable.Range(0, count).Select((i) =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        AppSettingsSection newSettings = (AppSettingsSection)builder.ProcessConfigurationSection(GetAppSettings());
                        Assert.Equal("TestKey1Value", newSettings.Settings["TestKey1"]?.Value);
                    }
                    catch (Exception e)
                    {
                        ex = e;
                    }
                });
            }
            );


            await Task.WhenAll(threads);

            Assert.Null(ex);

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

    class LazyConfigBuilder : KeyValueConfigBuilder
    {
        Dictionary<string, string> sourceValues;


        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            Console.Out.WriteLine($"LazyInitialise {DateTime.Now.ToLongTimeString()}");
            base.LazyInitialize(name, config);
            Console.Out.WriteLine($"LazyInitialise 1.5 {DateTime.Now.ToLongTimeString()}");
            Thread.Yield();

            // reference the getter from KeyValueConfigBuilder to ensure EnsureInitialised
            // doesn't cause a recursive loop
            var optional = Optional;
            // simulate a process that takes time to initialise
            Thread.Sleep(5000);


            sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                { "TestKey1", "TestKey1Value" },
                { "TestKey2", "TestKey2Value" },
                { "Prefix_TestKey", "Prefix_TestKeyValue" },
                { "Prefix_TestKey1", "Prefix_TestKey1Value" },
                { "Alt:Token", "ThisWasAnAlternateTokenPattern" },
                { "Alt_Token", "ThisWasADifferentAlternateTokenPattern" },
                { "Prefix_Alt:Token", "ThisWasAnAltTokenPatternWithPrefix" }
            };

        }

        public override string GetValue(string key)
        {
            if (sourceValues == null)
            {
                throw new Exception("This is not thread safe");
            }
            string value = null;
            return sourceValues.TryGetValue(key, out value) ? value : null;
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            if (sourceValues == null)
            {
                throw new Exception("This is not thread safe");
            }
            return sourceValues.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void SetTokenPattern(string newPattern)
        {
            this.TokenPattern = newPattern;
        }
    }

}
