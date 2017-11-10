using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Microsoft.Configuration.ConfigurationBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace Test
{
    [TestClass]
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
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"),
                $"GetValue[{name}]: Failed to retrieve existing value.");

            // Does not get what is not there.
            Assert.IsNull(builder.GetValue("This_Value_Does_Not_Exist"),
                $"GetValue[{name}]: Returned non-null value for a key that doesn't exist.");

            // Is NOT case-sensitive.
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], builder.GetValue("testkey"),
                $"GetValue[{name}]: Failed to retrieve existing value for case-insensitive key.");
        }

        public static void GetValue_Prefix1(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            builder.Initialize(name, attrs);

            // Does not care about prefix...
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"),
                $"GetValue_Prefix1[{name}]: Failed to retrieve first existing value.");
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"),
                $"GetValue_Prefix1[{name}]: Failed to retrieve second existing value.");
        }

        public static void GetValue_Prefix2(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            // or stripPrefix...
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"),
                $"GetValue_Prefix2[{name}]: Failed to retrieve first existing value.");
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"),
                $"GetValue_Prefix2[{name}]: Failed to retrieve second existing value.");
        }

        public static void GetValue_Prefix3(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            // even if there is no prefix given.
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], builder.GetValue("TestKey"),
                $"GetValue_Prefix3[{name}]: Failed to retrieve dirst existing value.");
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], builder.GetValue("Prefix_TestKey"),
                $"GetValue_Prefix3[{name}]: Failed to retrieve second existing value.");
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
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], GetValueFromCollection(allValues, "TestKey"),
                $"GetAllValues[{name}]: Failed to retrieve first existing value.");
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"),
                $"GetAllValues[{name}]: Failed to retrieve second existing value.");

            // Does not contain what is not there.
            // This would be a super wierd one to fail.
            Assert.IsNull(GetValueFromCollection(allValues, "This_Value_Does_Not_Exist"),
                $"GetAllValues[{name}]: Returned non-null value for a key that doesn't exist.");
        }

        public static void GetAllValues_Prefix1(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("prefix", "Prefix_");
            builder.Initialize(name, attrs);

            var allValues = builder.GetAllValues("Prefix_");

            // Has all the test values
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"),
                $"GetAllValues_Prefix1[{name}]: Failed to retrieve first existing value.");

            // Does not contain what is not there.
            Assert.IsNull(GetValueFromCollection(allValues, "TestKey"),
                $"GetAllValues_Prefix1[{name}]: Returned non-null value for a key that doesn't exist.");
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
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"),
                $"GetAllValues_Prefix2[{name}]: Failed to retrieve first existing value.");

            // Does not contain what is not there.
            Assert.IsNull(GetValueFromCollection(allValues, "TestKey"),
                $"GetAllValues_Prefix2[{name}]: Returned non-null value for a key that doesn't exist.");
        }

        public static void GetAllValues_Prefix3(KeyValueConfigBuilder builder, string name, NameValueCollection attrs = null)
        {
            attrs = attrs ?? new NameValueCollection();
            attrs.Add("stripPrefix", "true");
            builder.Initialize(name, attrs);

            var allValues = builder.GetAllValues("");

            // Has all the test values
            Assert.AreEqual(CommonKeyValuePairs["TestKey"], GetValueFromCollection(allValues, "TestKey"),
                $"GetAllValues_Prefix3[{name}]: Failed to retrieve first existing value.");
            Assert.AreEqual(CommonKeyValuePairs["Prefix_TestKey"], GetValueFromCollection(allValues, "Prefix_TestKey"),
                $"GetAllValues_Prefix3[{name}]: Failed to retrieve second existing value.");
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
