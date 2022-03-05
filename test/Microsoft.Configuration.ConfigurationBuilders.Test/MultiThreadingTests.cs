using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public class MultiThreadingTests
    {
        // This test likely produces an uncatchable StackOverflowException when it fails. Beware that
        // there likely won't be an explicit failure with this test. More like an 'incomplete.'
        [Fact]
        public void BigWorkInLazyInitialize_ThreadUsesNotinitializedObject()
        {
            //Arrange
            var builder = new SlowInitConfigBuilder();
            builder.Initialize("test", new NameValueCollection() { { "mode", "Token" } });
            var appSettings = new AppSettingsSection();
            appSettings.Settings.Add("${TestKey1}", "expandTestValue");

            //Act
            var task = Task.Run(() => builder.ProcessConfigurationSection(appSettings));
            var task2 = Task.Run(() =>
            {
                while (!builder.BaseInitialized) { }

                builder.ProcessConfigurationSection(appSettings);
            });
            Task.WaitAll(task, task2);

            //Assert
            Assert.False(builder.UseOfNotInititializedObject);
        }

        class SlowInitConfigBuilder : KeyValueConfigBuilder
        {
            private Dictionary<string, string> sourceValues;

            public bool UseOfNotInititializedObject { get; private set; } = false;
            public bool BaseInitialized { get; private set; } = false;

            /// <summary>
            /// Initializes the configuration builder lazily.
            /// </summary>
            /// <param name="name">The friendly name of the provider.</param>
            /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
            protected override void LazyInitialize(string name, NameValueCollection config)
            {
                base.LazyInitialize(name, config);
                BaseInitialized = true;

                // some big work here
                Task.Delay(2000).Wait();
                
                sourceValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    {"TestKey1", "TestKey1Value"}
                };
            }

            public override string GetValue(string key)
            {
                if (sourceValues == null)
                {
                    UseOfNotInititializedObject = true;
                }
                return sourceValues.TryGetValue(key, out var value) ? value : null;
            }

            public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
            {
                if (sourceValues == null)
                {
                    UseOfNotInititializedObject = true;
                }
                return sourceValues.Where(s => s.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }
    }
}