using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class OpenConfigTests : IDisposable
    {
        private static readonly string openConfigConfigTemplate = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
            <configuration>

              <configSections>
                <section name=""appSettings"" type=""System.Configuration.AppSettingsSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false""/>
                <section name=""connectionStrings"" type=""System.Configuration.ConnectionStringsSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false""/>
                <section name=""configBuilders"" type=""System.Configuration.ConfigurationBuildersSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false"" />
                <section name=""Microsoft.Configuration.ConfigurationBuilders.SectionHandlers"" type=""Microsoft.Configuration.ConfigurationBuilders.SectionHandlersSection, Microsoft.Configuration.ConfigurationBuilders.Base"" restartOnExternalChanges=""false"" requirePermission=""false"" />
              </configSections>

              <configBuilders>
                <builders>
                  ###DEFINED_BUILDERS###
                </builders>
              </configBuilders>

              ###APP_SETTINGS###

              ###CONN_STRS###

            </configuration>";
        private readonly string jsonTestFileName = Path.Combine(Environment.CurrentDirectory, "testConfigFiles", "simpleJsonOpenConfigTest.json");


        [Fact]
        public void Same_Section_Strict_AppSettings_Parameters()
        {
            string builders = @"
                <add name=""Json"" mode=""Strict"" jsonMode=""Sectional"" jsonFile=""${jsonFile}"" enabled=""enabled"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string appSettings = $@"
                <appSettings configBuilders=""Json"">
                  <add key=""jsonFile"" value=""{jsonTestFileName}"" />
                  <add key=""strictSetting"" value=""originalStrictValue"" />
                </appSettings>
            ";
            string connStrs = $@"";
            string configString = openConfigConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APP_SETTINGS###", appSettings).Replace("###CONN_STRS###", connStrs);
            var config = LoadConfigFromString(configString);

            Assert.Equal(2, config.AppSettings.Settings.Count);
            Assert.Equal("newStrictValue", config.AppSettings.Settings["strictSetting"].Value);
        }

        [Fact]
        public void Same_Section_Greedy_AppSettings_Parameters()
        {
            string builders = @"
                <add name=""Json"" mode=""Greedy"" jsonMode=""Sectional"" jsonFile=""${jsonFile}"" enabled=""enabled"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string appSettings = $@"
                <appSettings configBuilders=""Json"">
                  <add key=""jsonFile"" value=""{jsonTestFileName}"" />
                  <add key=""strictSetting"" value=""originalStrictValue"" />
                </appSettings>
            ";
            string connStrs = $@"";
            string configString = openConfigConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APP_SETTINGS###", appSettings).Replace("###CONN_STRS###", connStrs);
            var config = LoadConfigFromString(configString);

            Assert.Equal(4, config.AppSettings.Settings.Count);
            Assert.Equal("newStrictValue", config.AppSettings.Settings["strictSetting"].Value);
            Assert.Equal("newGreedyValue", config.AppSettings.Settings["greedySetting"].Value);
            Assert.Equal("not-enabled-cause-exception", config.AppSettings.Settings["csJsonEnabled"].Value);
        }

        [Fact]
        public void Cross_Section_Plain_AppSettings_Parameters()
        {
            string builders = @"
                <add name=""Json"" mode=""Greedy"" jsonMode=""Sectional"" jsonFile=""${jsonFile}"" enabled=""enabled"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string appSettings = $@"
                <appSettings>
                  <add key=""jsonFile"" value=""{jsonTestFileName}"" />
                </appSettings>
            ";
            string connStrs = $@"
                <connectionStrings configBuilders=""Json"">
                  <add name=""connStr1"" connectionString=""invalid original value"" />
                </connectionStrings>
            ";
            string configString = openConfigConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APP_SETTINGS###", appSettings).Replace("###CONN_STRS###", connStrs);
            var config = LoadConfigFromString(configString);

            Assert.Equal(3, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("old-style connStr only", config.ConnectionStrings.ConnectionStrings["connStr1"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr1"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr2:connectionString"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2:connectionString"].ProviderName);
            Assert.Equal("A value for cs2 provider name from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr2:providerName"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2:providerName"].ProviderName);
        }

        [Fact]
        public void Cross_Section_Dynamic_AppSettings_Parameters()
        {
            string builders = @"
                <add name=""Json1"" mode=""Greedy"" jsonMode=""Sectional"" jsonFile=""${jsonFile}"" enabled=""enabled"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
                <add name=""Json2"" mode=""Strict"" jsonMode=""Sectional"" jsonFile=""a-bad-file-name.json"" enabled=""${csJsonEnabled}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string appSettings = $@"
                <appSettings configBuilders=""Json1"">
                  <add key=""jsonFile"" value=""{jsonTestFileName}"" />
                </appSettings>
            ";
            string connStrs = $@"
                <connectionStrings configBuilders=""Json2"">
                  <add name=""connStr1"" connectionString=""invalid original value"" />
                </connectionStrings>
            ";
            string configString = openConfigConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APP_SETTINGS###", appSettings).Replace("###CONN_STRS###", connStrs);
            var config = LoadConfigFromString(configString);

            // To make sure Json2 is getting the correct values from Json1, we have mis-configured it. It should throw an exception
            // regardless. But the content of the exception will let us know if it passed or failed.
            var ex = Assert.Throws<ConfigurationErrorsException>(() => config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Contains("Json2", ex.Message);
            Assert.Contains("not-enabled-cause-exception", ex.Message);
            Assert.DoesNotContain("Json1", ex.Message);
            Assert.DoesNotContain("a-bad-file-name.json", ex.Message);
        }

        [Fact]
        public void NonDefault_SectionHandlers()
        {
            string builders = $@"
                <add name=""Json"" mode=""Greedy"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" enabled=""enabled"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            // Ok, faking it a little here
            string appSettings = @"
              <Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
                <handlers>
                  <remove name=""DefaultConnectionStringsHandler"" />
                  <add name=""NewConnectionStringsHandler"" type=""Microsoft.Configuration.ConfigurationBuilders.ConnectionStringsSectionHandler2, Microsoft.Configuration.ConfigurationBuilders.Base"" />
                </handlers>
              </Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
            ";
            string connStrs = $@"
                <connectionStrings configBuilders=""Json"">
                  <add name=""connStr1"" connectionString=""invalid original value"" />
                </connectionStrings>
            ";
            string configString = openConfigConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APP_SETTINGS###", appSettings).Replace("###CONN_STRS###", connStrs);
            var config = LoadConfigFromString(configString);

            Assert.Equal(2, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("old-style connStr only", config.ConnectionStrings.ConnectionStrings["connStr1"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr1"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("A value for cs2 provider name from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
        }


        private List<string> tempFilesToCleanup = new List<string>();
        private Configuration LoadConfigFromString(string configString)
        {
            string cfg = configString;
            var config = TestHelper.LoadConfigFromString(ref cfg);
            tempFilesToCleanup.Add(cfg);
            return config;
        }

        // ======================================================================
        //   IDisposable Pattern
        // ======================================================================
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                foreach (string filename in tempFilesToCleanup)
                    if (!String.IsNullOrEmpty(filename))
                        File.Delete(filename);

                disposedValue = true;
            }
        }

        // override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        ~OpenConfigTests()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
