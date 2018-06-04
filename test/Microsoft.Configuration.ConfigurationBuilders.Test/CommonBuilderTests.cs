using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class CommonBuilderTests
    {
        public static NameValueCollection CommonKeyValuePairs = new NameValueCollection() {
            { "TestKey", "TestValue1" },
            { "Prefix_TestKey", "testvalue2" }
        };

        // ======================================================================
        //   GetValue
        //      - Gets what is there.
        //      - Does not get what is not there.
        //      - Is NOT case-sensitive.
        //      - Does not care about prefix or stripPrefix.
        // ======================================================================

        public static void GetValue(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            builder.Initialize(name, attrs ?? new NameValueCollection());

            // Gets what is there.
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));

            // Does not get what is not there.
            Assert.Null(builder.GetValue("This_Value_Does_Not_Exist"));

            // Is NOT case-sensitive.
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("testkey"));
        }

        public static void GetValue_Prefix1(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            builder.Initialize(name, attrs);

            // Does not care about prefix...
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"));
        }

        public static void GetValue_Prefix2(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            // or stripPrefix...
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"));
        }

        public static void GetValue_Prefix3(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            // even if there is no prefix given.
            Assert.Equal(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"));
        }


        // ======================================================================
        //   GetAllValues
        //      - Has existing values
        //      - Does not contain what is not there.
        // ======================================================================
        public static void GetAllValues(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            builder.Initialize(name, attrs ?? new NameValueCollection());

            var allValues = builder.GetAllValues("");

            // Has all the test values
            Assert.Equal(CommonKeyValuePairs["TestKey"], GetValueFromCollection(allValues, "TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"));

            // Does not contain what is not there.
            // This would be a super wierd one to fail.
            Assert.Null(GetValueFromCollection(allValues, "This_Value_Does_Not_Exist"));
        }

        public static void GetAllValues_Prefix1(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            builder.Initialize(name, attrs);

            var allValues = builder.GetAllValues("Prefix_");

            // Has all the test values
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"));

            // Does not contain what is not there.
            Assert.Null(GetValueFromCollection(allValues, "TestKey"));
        }

        public static void GetAllValues_Prefix2(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            // stripPrefix does not affect GetAllValues, as the KVCB base handles all prefix-stripping tasks.
            var allValues = builder.GetAllValues("Prefix_");

            // Has all the test values
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"));

            // Does not contain what is not there.
            Assert.Null(GetValueFromCollection(allValues, "TestKey"));
        }

        public static void GetAllValues_Prefix3(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            var allValues = builder.GetAllValues("");

            // Has all the test values
            Assert.Equal(CommonKeyValuePairs["TestKey"], GetValueFromCollection(allValues, "TestKey"));
            Assert.Equal(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"));
        }




        // ======================================================================
        //  Helpers
        // ======================================================================
        private static string GetValueFromCollection(ICollection<KeyValuePair<string, string>> collection, string key)
        {
            foreach (var kvp in collection)
            {
                if (kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }
    }
}
