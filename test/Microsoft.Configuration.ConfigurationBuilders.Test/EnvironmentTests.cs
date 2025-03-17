using System;
using System.Collections.Specialized;
using System.IO;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class EnvironmentFixture : IDisposable
    {
        // This doesn't really need to be a Fixtures - since there isn't really any cleanup that has to be done.
        // But it does save us from repeated setup, and also matches the pattern we use in the other builder tests.
        public EnvironmentFixture()
        {
            // Populate the environment with key/value pairs that are needed for common tests
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                Environment.SetEnvironmentVariable(key, CommonBuilderTests.CommonKeyValuePairs[key]);
        }

        public void Dispose() { }
    }

    public class EnvironmentTests : IClassFixture<EnvironmentFixture>
    {
        private readonly EnvironmentFixture _fixture;

        public EnvironmentTests(EnvironmentFixture fixture)
        {
            _fixture = fixture;
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

        [Fact]
        public void Environment_ProcessConfigurationSection()
        {
            // We have to use a prefix to filter out non-related environment variables :/
            CommonBuilderTests.ProcessConfigurationSection(() => new EnvironmentConfigBuilder(), "EnvProcessConfig",
                new NameValueCollection() { { "prefix", CommonBuilderTests.CommonKVPrefix } });
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
