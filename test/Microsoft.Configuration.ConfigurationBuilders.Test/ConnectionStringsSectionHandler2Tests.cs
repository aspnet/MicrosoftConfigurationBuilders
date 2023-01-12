using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;


namespace Test
{
    public class ConnectionStringsSectionHandler2Tests : IDisposable
    {
        private static readonly string cssh2ConfigTemplate = @"<?xml version=""1.0"" encoding=""utf-8"" ?>
<configuration>

  <configSections>
    <section name=""connectionStrings"" type=""System.Configuration.ConnectionStringsSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false""/>
    <section name=""configBuilders"" type=""System.Configuration.ConfigurationBuildersSection, System.Configuration, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"" restartOnExternalChanges=""false"" requirePermission=""false"" />
    <section name=""Microsoft.Configuration.ConfigurationBuilders.SectionHandlers"" type=""Microsoft.Configuration.ConfigurationBuilders.SectionHandlersSection, Microsoft.Configuration.ConfigurationBuilders.Base"" restartOnExternalChanges=""false"" requirePermission=""false"" />
  </configSections>

  <Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>
    <handlers>
      <remove name=""DefaultConnectionStringsHandler"" />
      <add name=""NewConnectionStringsHandler"" type=""Microsoft.Configuration.ConfigurationBuilders.ConnectionStringsSectionHandler2, Microsoft.Configuration.ConfigurationBuilders.Base"" />
    </handlers>
  </Microsoft.Configuration.ConfigurationBuilders.SectionHandlers>

  <configBuilders>
    <builders>
      ###DEFINED_BUILDERS###
    </builders>
  </configBuilders>

  <connectionStrings configBuilders=""###APPLIED_BUILDERS###"">
    <add name=""connStr0"" connectionString=""pre-existing 0 connStr"" providerName=""should not be touched"" />
    <add name=""connStr2"" connectionString=""pre-existing 2 connStr"" />
    <add name=""connStr3"" connectionString=""pre-existing 3 connStr"" providerName=""pre-defined 3 pName"" />
    <add name=""${aNameFromJson}"" connectionString=""${connStr2:connectionString}"" providerName=""${connStr3:connectionString}"" />
    <add name=""aNameFromJson"" connectionString=""${token_value}"" providerName=""leave me alone"" />
    <add name=""weird"" connectionString=""${aNameFromJson}"" />
  </connectionStrings>

</configuration>";
        private readonly string jsonTestFileName = Path.Combine(Environment.CurrentDirectory, "testConfigFiles", "simpleJsonConnStrTest.json");
        private List<string> tempFilesToCleanup = new List<string>();

        [Fact]
        public void CSSH2_Strict()
        {
            string builders = $@"
                <add name=""Json"" mode=""Strict"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("A value for cs2 provider name from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("${connStr2:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ConnectionString);
            Assert.Equal("${connStr3:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ProviderName);
            Assert.Equal("Contains_a_${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("${aNameFromJson}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }

        [Fact]
        public void CSSH2_StrictSection()
        {
            string builders = $@"
                <add name=""Json"" mode=""Strict"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection - no provider name", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("Just a simple CS from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("${connStr2:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ConnectionString);
            Assert.Equal("${connStr3:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ProviderName);
            Assert.Equal("${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("${aNameFromJson}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }

        [Fact]
        public void CSSH2_GreedySectional()
        {
            string builders = $@"
                <add name=""Json"" mode=""Greedy"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6 + 5, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection - no provider name", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("Just a simple CS from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("${connStr2:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ConnectionString);
            Assert.Equal("${connStr3:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ProviderName);
            Assert.Equal("${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("${aNameFromJson}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);

            Assert.Equal("Remember this is a flat key/value config source - this random is its own key/value", config.ConnectionStrings.ConnectionStrings["connStr1:randomAttr"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr1:randomAttr"].ProviderName);
            Assert.Equal("A value for cs1 from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr1"].ConnectionString);
            Assert.Equal("A value for cs1 provider name from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr1"].ProviderName);
            Assert.Equal("This is weird", config.ConnectionStrings.ConnectionStrings["connectionString"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connectionString"].ProviderName);
            Assert.Equal("Brought to you by weird", config.ConnectionStrings.ConnectionStrings["providerName"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["providerName"].ProviderName);
            Assert.Equal("NOT_A_TOKEN_ANYMORE", config.ConnectionStrings.ConnectionStrings["token_value"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["token_value"].ProviderName);
        }

        [Fact]
        public void CSSH2_Token()
        {
            string builders = $@"
                <add name=""Json"" mode=""Token"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("pre-existing 2 connStr", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("pre-existing 3 connStr", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ConnectionString);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ProviderName);
            Assert.Equal("${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("Contains_a_${token_value}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }

        [Fact]
        public void CSSH2_Token_StrictSectional()
        {
            string builders = $@"
                <add name=""Json1"" mode=""Token"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
                <add name=""Json2"" mode=""Strict"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json1,Json2";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection - no provider name", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("Just a simple CS from the CS subsection", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ConnectionString);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ProviderName);
            Assert.Equal("${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("Contains_a_${token_value}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }

        [Fact]
        public void CSSH2_Token_Greedy()
        {
            string builders = $@"
                <add name=""Json1"" mode=""Token"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
                <add name=""Json2"" mode=""Greedy"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json1,Json2";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6 + 3 + 6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("A value for cs2 provider name from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("Weird double replaced value but not token", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ConnectionString);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["Contains_a_${token_value}"].ProviderName);
            Assert.Equal("Contains_a_${token_value}", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("Contains_a_${token_value}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);

            Assert.Equal("Just a single string at the root", config.ConnectionStrings.ConnectionStrings["connStr1"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr1"].ProviderName);
            Assert.Equal("A random attribute isn't actually ignored", config.ConnectionStrings.ConnectionStrings["connStr2:randomAttr"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2:randomAttr"].ProviderName);
            Assert.Equal("Yes, we can fake it like this in the root", config.ConnectionStrings.ConnectionStrings["connStr4"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr4"].ProviderName);

            Assert.Equal("Remember this is a flat key/value config source - this random is its own key/value", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr1:randomAttr"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr1:randomAttr"].ProviderName);
            Assert.Equal("A value for cs1 from the CS subsection", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr1"].ConnectionString);
            Assert.Equal("A value for cs1 provider name from the CS subsection", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr1"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection - no provider name", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr2"].ProviderName);
            Assert.Equal("Just a simple CS from the CS subsection", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr3"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connectionStrings:connStr3"].ProviderName);
            Assert.Equal("This is weird", config.ConnectionStrings.ConnectionStrings["connectionStrings"].ConnectionString);
            Assert.Equal("Brought to you by weird", config.ConnectionStrings.ConnectionStrings["connectionStrings"].ProviderName);
            Assert.Equal("NOT_A_TOKEN_ANYMORE", config.ConnectionStrings.ConnectionStrings["connectionStrings:token_value"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connectionStrings:token_value"].ProviderName);
        }

        [Fact]
        public void CSSH2_Token_TokenSectional()
        {
            string builders = $@"
                <add name=""Json1"" mode=""Token"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
                <add name=""Json2"" mode=""Token"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json1,Json2";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("pre-existing 2 connStr", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("pre-existing 3 connStr", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["Contains_a_NOT_A_TOKEN_ANYMORE"].ConnectionString);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["Contains_a_NOT_A_TOKEN_ANYMORE"].ProviderName);
            Assert.Equal("NOT_A_TOKEN_ANYMORE", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("Contains_a_NOT_A_TOKEN_ANYMORE", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }

        [Fact]
        public void CSSH2_Strict_TokenSectional()
        {
            string builders = $@"
                <add name=""Json1"" mode=""Strict"" jsonMode=""Flat"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
                <add name=""Json2"" mode=""Token"" jsonMode=""Sectional"" jsonFile=""{jsonTestFileName}"" type=""Microsoft.Configuration.ConfigurationBuilders.SimpleJsonConfigBuilder, Microsoft.Configuration.ConfigurationBuilders.Json"" />
            ";
            string builderList = "Json1,Json2";
            string configString = cssh2ConfigTemplate.Replace("###DEFINED_BUILDERS###", builders).Replace("###APPLIED_BUILDERS###", builderList);
            var config = LoadConfigFromString(configString);

            Assert.Equal(6, config.ConnectionStrings.ConnectionStrings.Count);
            Assert.Equal("pre-existing 0 connStr", config.ConnectionStrings.ConnectionStrings["connStr0"].ConnectionString);
            Assert.Equal("should not be touched", config.ConnectionStrings.ConnectionStrings["connStr0"].ProviderName);
            Assert.Equal("A value for cs2 from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ConnectionString);
            Assert.Equal("A value for cs2 provider name from the root", config.ConnectionStrings.ConnectionStrings["connStr2"].ProviderName);
            Assert.Equal("A value for cs3 from the root - no provider name", config.ConnectionStrings.ConnectionStrings["connStr3"].ConnectionString);
            Assert.Equal("pre-defined 3 pName", config.ConnectionStrings.ConnectionStrings["connStr3"].ProviderName);
            Assert.Equal("A value for cs2 from the CS subsection - no provider name", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ConnectionString);
            Assert.Equal("${connStr3:connectionString}", config.ConnectionStrings.ConnectionStrings["${aNameFromJson}"].ProviderName);
            Assert.Equal("Contains_a_NOT_A_TOKEN_ANYMORE", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ConnectionString);
            Assert.Equal("leave me alone", config.ConnectionStrings.ConnectionStrings["aNameFromJson"].ProviderName);
            Assert.Equal("${aNameFromJson}", config.ConnectionStrings.ConnectionStrings["weird"].ConnectionString);
            Assert.Equal("", config.ConnectionStrings.ConnectionStrings["weird"].ProviderName);
        }



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
        ~ConnectionStringsSectionHandler2Tests()
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
