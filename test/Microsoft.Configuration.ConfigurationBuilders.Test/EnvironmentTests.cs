﻿using System;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
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
        [Fact]
        public void Environment_GetValue()
        {
            CommonBuilderTests.GetValue(() => new EnvironmentConfigBuilder(), "EnvGetValue");
        }

        [Fact]
        public void Environment_GetAllValues()
        {
            CommonBuilderTests.GetAllValues(() => new EnvironmentConfigBuilder(), "EnvGetAll");
        }

        // ======================================================================
        //   Environment Parameters
        // ======================================================================
        [Fact]
        public void Environment_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<EnvironmentConfigBuilder>(() => new EnvironmentConfigBuilder(), "EnvDefault");

            // Enabled
            Assert.Equal(KeyValueEnabled.Optional, builder.Enabled);

            // CharacterMap
            Assert.Single(builder.CharacterMap);
            Assert.Equal("__", builder.CharacterMap[":"]);
        }
    }
}
