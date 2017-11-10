using System;
using Microsoft.Configuration.ConfigurationBuilders;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace Test
{
    [TestClass]
    public class EnvironmentTests
    {
        public EnvironmentTests()
        {
            // Populate the environment with key/value pairs that are needed for common tests
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                Environment.SetEnvironmentVariable(key, CommonBuilderTests.CommonKeyValuePairs[key]);
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [TestMethod]
        public void Environment_GetValue()
        {
            CommonBuilderTests.GetValue(new EnvironmentConfigBuilder(), "EnvironmentBuilder");
            CommonBuilderTests.GetValue_Prefix1(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix1");
            CommonBuilderTests.GetValue_Prefix2(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix2");
            CommonBuilderTests.GetValue_Prefix3(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix3");
        }

        [TestMethod]
        public void Environment_GetAllValues()
        {
            CommonBuilderTests.GetAllValues(new EnvironmentConfigBuilder(), "EnvironmentBuilder");
            CommonBuilderTests.GetAllValues_Prefix1(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix1");
            CommonBuilderTests.GetAllValues_Prefix2(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix2");
            CommonBuilderTests.GetAllValues_Prefix3(new EnvironmentConfigBuilder(), "EnvironmentBuilderPrefix3");
        }
    }
}
