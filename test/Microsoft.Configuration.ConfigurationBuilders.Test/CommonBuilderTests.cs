using System;
using System.Collections.Specialized;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class CommonBuilderTests
    {
        public static NameValueCollection CommonKeyValuePairs = new NameValueCollection() {
            { "TestKey", "TestValue1" },
            { "Prefix-TestKey", "testvalue2" },
            { "Value-Needs-Escaping", "Value \'with\" question@ble C#ar&ct*rs <in> it." }
        };

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
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));

            // Does not get what is not there.
            Assert.Null(builder.GetValue("This-Value-Does-Not-Exist"));

            // Is NOT case-sensitive.
            if (!caseSensitive)
                Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("testkey"));
            // Or maybe it is for some reason. (Looking at you Azure App Config.)
            else
                Assert.Null(builder.GetValue("testkey"));

            // Does not care about prefix...
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("prefix", "Prefix-");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], builder.GetValue("Prefix-TestKey"));

            // ...or stripPrefix...
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("prefix", "Prefix-");
            customSettings.Add("stripPrefix", "true");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], builder.GetValue("Prefix-TestKey"));

            // ...even if there is no prefix given.
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("stripPrefix", "true");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], builder.GetValue("Prefix-TestKey"));

            // Does not escape values.
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("escapeExpandedValues", "true");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], builder.GetValue("Prefix-TestKey"));
            Assert.Equal(CommonKeyValuePairs["Value-Needs-Escaping"], builder.GetValue("Value-Needs-Escaping"));

            // Does not care about charMap.
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("charMap", "e=@");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Null(builder.GetValue("T@stK@y"));
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], builder.GetValue("Prefix-TestKey"));
            Assert.Null(builder.GetValue("Pr@fix-T@stK@y"));
        }

        // ======================================================================
        //   GetAllValues
        //      - Has existing values.
        //      - Does not contain what is not there.
        // ======================================================================
        public static void GetAllValues(Func<KeyValueConfigBuilder> builderFactory, string name, NameValueCollection settings = null)
        {
            NameValueCollection customSettings, baseSettings = settings ?? new NameValueCollection();
            KeyValueConfigBuilder builder = TestHelper.CreateBuilder(builderFactory, name, baseSettings);

            // Has all the test values.
            var allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], TestHelper.GetValueFromCollection(allValues, key));

            // Does not contain what is not there.
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "This-Value-Does-Not-Exist"));

            // =============================================================================
            // Works with Prefix
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("prefix", "Prefix-");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("Prefix-");

            // Has all the test values.
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], TestHelper.GetValueFromCollection(allValues, "Prefix-TestKey"));

            // Does not contain what is not there.
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "TestKey"));

            // =========================================================================================
            // Works with Prefix... and Strip has no effect (KVCB base handles all stripping tasks.)
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("prefix", "Prefix-");
            customSettings.Add("stripPrefix", "true");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("Prefix-");

            // Has all the test values.
            Assert.Equal(CommonKeyValuePairs["Prefix-TestKey"], TestHelper.GetValueFromCollection(allValues, "Prefix-TestKey"));

            // Does not contain what is not there.
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "TestKey"));

            // =========================================================================================
            // Does not escape values.
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("escapeExpandedValues", "true");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], TestHelper.GetValueFromCollection(allValues, key));

            // Does not care about charMap.
            customSettings = new NameValueCollection(baseSettings);
            customSettings.Add("charMap", "e=@");
            builder = TestHelper.CreateBuilder(builderFactory, name, customSettings);
            allValues = builder.GetAllValues("");
            foreach (var key in CommonKeyValuePairs.AllKeys)
                Assert.Equal(CommonKeyValuePairs[key], TestHelper.GetValueFromCollection(allValues, key));
            Assert.Null(TestHelper.GetValueFromCollection(allValues, "T@stK@y"));
        }
    }
}
