using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;

using Azure.Data.AppConfiguration;
using Azure.Identity;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AppConfigFactAttribute : FactAttribute
    {
        public AppConfigFactAttribute(string Reason = null)
        {
            if (!AzureAppConfigTests.AppConfigTestsEnabled)
                Skip = Reason ?? "Skipped: Azure AppConfig Tests Disabled.";
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AppConfigTheoryAttribute : TheoryAttribute
    {
        public AppConfigTheoryAttribute(string Reason = null)
        {
            if (!AzureAppConfigTests.AppConfigTestsEnabled)
                Skip = Reason ?? "Skipped: Azure AppConfig Tests Disabled.";
        }
    }

    public class AzureAppConfigTests
    {
        private static readonly string commonEndPoint;
        private static readonly string customEndPoint;
        private static readonly DateTimeOffset customEpoch;

        // There is probably a better way of doing this. But we're just piggy-backing off of the
        // custom Key Vault we use for the AzureKeyVaultConfigBuilder test suite. We assume the
        // values of secrets in those tests. Do the same here.
        private readonly string kva_value_old = "versionedValue-Older";
        private readonly string kva_value_new = "versionedValue-Current";
        private readonly string kvb_value = "mappedValue";
        private readonly string kvUriRegex = "{\"uri\":\".+\"}";


        // Update this to true to enable AzConfig tests.
        public static bool AppConfigTestsEnabled => true;
        // Update this to true if the structure of the config store matches what is described below.
        // OTOH, if the 'history' of the entries has been lost (cleared after 7 or 30 days depending
        // on the subscription plan) then only the most recent values remain and test verification
        // will neccessarily be different. Set this to false if 'history' has been cleared away.
        public static bool AzConfigHistoryInTact => false;

        static AzureAppConfigTests()
        {
            // The Common config store gets filled out, but the store itself is assumed to already exist.
            commonEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Common");
            ConfigurationClient cfgClient = new ConfigurationClient(new Uri(commonEndPoint), new DefaultAzureCredential());
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                // No need to check for the setting first. We're going to write over it anyway to make sure
                // it is free of labels and such.
                ConfigurationSetting setting = new ConfigurationSetting(key, CommonBuilderTests.CommonKeyValuePairs[key], null);
                cfgClient.SetConfigurationSetting(setting, false);  // Overwrite without exception
            }

            // The Custom config store is assumed to exist and already be filled out. The key vault references
            // leverage the custom key vault used in the KV config builder tests.
            //
            //      kva:    versioned-key == versionedValue-Current
            //                               versionedValue-Older
            //      kvb:    mapped-test-key == mappedValue

            // Time -->                 Beginning       (labelA)      |epoch|               (labelA)        (labelB)
            // epochDTO                                                     DateTimeOffset-of-the-epoch (show up after epoch)
            // caseTestSetting                          altCaseTestValue    newCaseTestValue
            // testSetting              oldTestValue    altTestValue        newTestValue    newAltValue
            // newTestSetting                                               ntOGValue       ntValueA
            // superTestSetting         oldSuperValue                                       newSuperAlpha   newSuperBeta
            // keyVaultSetting          kva_value_old   kva_value_new       kva_value_old   kvb_value
            // superKeyVaultSetting     kvb_value                           kva_value_old                   kva_value_new
            //
            //      curious about                                                           onlyNewLabA     andNewLabB
            customEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Custom");
            cfgClient = new ConfigurationClient(new Uri(customEndPoint), new DefaultAzureCredential());
            customEpoch = DateTimeOffset.Parse(cfgClient.GetConfigurationSetting("epochDTO").Value.Value);
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [AppConfigFact]
        public void AzureAppConfig_GetValue()
        {
            CommonBuilderTests.GetValue(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGetValue",
                new NameValueCollection() { { "endpoint", commonEndPoint } }, true);
        }

        [AppConfigFact]
        public void AzureAppConfig_GetAllValues()
        {
            CommonBuilderTests.GetValue(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGetAllValues",
                new NameValueCollection() { { "endpoint", commonEndPoint } }, true);
        }

        // ======================================================================
        //   AzureAppConfig parameters
        // ======================================================================
        [AppConfigFact]
        public void AzureAppConfig_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigDefault",
                new NameValueCollection() { { "endpoint", customEndPoint } });

            // Endpoint matches, ConnectionString is null
            Assert.Equal(customEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);

            // KeyFilter
            Assert.Null(builder.KeyFilter);

            // LabelFilter
            Assert.Null(builder.LabelFilter);

            // DateTimeFilter
            Assert.Equal(DateTimeOffset.MinValue, builder.AcceptDateTime);

            // UseAzureKeyVault
            Assert.False(builder.UseAzureKeyVault);

            // Enabled
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // CharMap
            Assert.Empty(builder.CharacterMap);
        }


        [AppConfigFact]
        public void AzureAppConfig_Settings()
        {
            // Endpoint is case insensitive, connection string is null
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings1",
                new NameValueCollection() { { "EndpOInt", customEndPoint } });
            Assert.Equal(customEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);

            // ConnectionString is case insensitive, Endpoint is null
            // Note: The current implementation allows a bad connection string to be an optional failure.
            //    We only require an endpoint for this test suite. We do not have a valid connection string.
            //    That's ok. Make the builder optional, and we can still verify the values got read, even
            //    if they are not good values.
            var fakeCS = "This-Is_Not;A:Valid/Connec+ion=Str|ng.";
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings2",
                new NameValueCollection() { { "COnneCtiOnStrinG", fakeCS }, { "enabled", KeyValueEnabled.Optional.ToString() } });
            Assert.Equal(fakeCS, builder.ConnectionString);
            Assert.Null(builder.Endpoint);

            // Both Endpoint and ConnectionString given, Endpoint used, ConnectionString null
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings3",
                new NameValueCollection() { { "connectionString", fakeCS }, { "endpoint", customEndPoint } });
            Assert.Equal(customEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);

            // UseKeyVault is case insensitive, allows reading KeyVault values
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings4",
                new NameValueCollection() { { "endpoint", customEndPoint }, { "UsEaZuReKeYvAuLt", "tRUe" } });
            Assert.Equal(customEndPoint, builder.Endpoint);
            Assert.True(builder.UseAzureKeyVault);
            Assert.Equal(kva_value_old, builder.GetValue("keyVaultSetting"));
            var allValues = builder.GetAllValues("");
            Assert.Equal(kva_value_old, TestHelper.GetValueFromCollection(allValues, "superKeyVaultSetting"));

            // UseKeyVault is case insensitive, does not allow reading KeyVault values
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings5",
                new NameValueCollection() { { "endpoint", customEndPoint }, { "useAzureKeyVault", "false" } });
            Assert.Equal(customEndPoint, builder.Endpoint);
            Assert.False(builder.UseAzureKeyVault);
            Assert.Matches(kvUriRegex, builder.GetValue("keyVaultSetting"));    // Don't care what the uri is... just that is it a URI instead of a value
            allValues = builder.GetAllValues("");
            Assert.Matches(kvUriRegex, TestHelper.GetValueFromCollection(allValues, "superKeyVaultSetting"));
        }

        public static IEnumerable<object[]> GetFilterTestParameters()
        {
            foreach (KeyValueMode mode in new [] { KeyValueMode.Strict, KeyValueMode.Greedy })
            {
                foreach (var dto in new object[] { null, DateTimeOffset.MinValue, customEpoch, DateTimeOffset.MaxValue })
                {
                    yield return new object[] { mode, null, null, dto, false };
                    yield return new object[] { mode, "", "", dto, true };
                    yield return new object[] { mode, "super*", null, dto, true };
                    yield return new object[] { mode, null, "labelA", dto, true };
                    yield return new object[] { mode, "super*", "labelA", dto, false };
                }
            }
        }

        [AppConfigTheory]
        [MemberData(nameof(GetFilterTestParameters))]
        public void AzureAppConfig_Filters(KeyValueMode mode, string keyFilter, string labelFilter, DateTimeOffset dtFilter, bool useAzure)
        {
            bool isOld = (dtFilter == customEpoch);

            // Trying all sorts of combinations with just one test and lots of theory data
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigFilters",
                new NameValueCollection() { { "endpoint", customEndPoint }, { "mode", mode.ToString() }, { "keyFilter", keyFilter }, { "labelFilter", labelFilter },
                                            { "acceptDateTime", dtFilter.ToString() }, { "useAzureKeyVault", useAzure.ToString() }});
            ValidateExpectedConfig(builder, isOld);

            // TODO: Specifying KeyFilter in a non-Greedy mode triggers GreedyInit
            // Case insensitive attribute names? Case sensitive filters?
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        [AppConfigTheory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void AzureKeyVault_ErrorsOptional(KeyValueEnabled enabled)
        {
            AzureAppConfigurationBuilder builder = null;

            // No endpoint or connection string
            var exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors1", "ndpoint", "must be provided");
            else
                Assert.Null(exception);

            // Invalid endpoint
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors2",
                    new NameValueCollection() { { "endpoint", "this-will-not-work" }, { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors2", "xception encountered while creating connection to Azure App Configuration store");
            else
                Assert.Null(exception);

            // Invalid connection string
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors3",
                    new NameValueCollection() { { "connectionString", "this-will-not-work" }, { "enabled", enabled.ToString() } });
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors3", "xception encountered while creating connection to Azure App Configuration store");
            else
                Assert.Null(exception);

            // Invalid labelFilter (* and , are not allowed)
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors4",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "labelFilter", "invalid*" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors4", "not supported in label filters");
            else
                Assert.Null(exception);
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors5",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "labelFilter", "invalid,comma" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors5", "not supported in label filters");
            else
                Assert.Null(exception);

            // Invalid acceptDateTime (Not a DateTimeOffset)
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors6",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "acceptDateTime", "neither a date nor time" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<FormatException>(exception, "AzureAppConfigErrors6");
            else
                Assert.Null(exception);

            // Invalid useAzureKeyVault
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors7",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "useAzureKeyVault", "neither tru nor fals" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<FormatException>(exception, "AzureAppConfigErrors7");
            else
                Assert.Null(exception);

            // Invalid key name (can't be '.', '..', or contain '%'... not that we care, but we should handle the error gracefully)
            // Oddly, AppConfig doesn't return an error here. Just quietly returns nothing. So expect no exception.
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors8",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "enabled", enabled.ToString() } });
                Assert.Null(builder.GetValue("bad%keyname"));
            });
            Assert.Null(exception);

            // Invalid key character with key filter (use of key filter should trigger greedy-style pre-load even in strict mode)
            // Oddly again, AppConfig doesn't return an error here. Just quietly returns nothing. So expect no exception.
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors9",
                    new NameValueCollection() { { "endpoint", customEndPoint }, { "keyFilter", "bad%Filter*" }, { "enabled", enabled.ToString() } });
                Assert.Null(builder.GetValue("bad%FilterFetchesThisValue"));
            });
            Assert.Null(exception);

            // TODO: Can we produce an invalid KeyVault reference? That should throw an error.
        }

        // ======================================================================
        //   Helpers
        // ======================================================================
        // TODO: Mock ConfigurationClient. Much work, and we'd need to inject it into the builder.
        private void ValidateExpectedConfig(AzureAppConfigurationBuilder builder, bool oldTimes)
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;

            // Prime the appSettings section as expected for this test
            appSettings.Settings.Add("casetestsetting", "untouched");
            if (builder.Mode != KeyValueMode.Greedy)
            {
                appSettings.Settings.Add("testSetting", "untouched");
                appSettings.Settings.Add("newTestSetting", "untouched");
                appSettings.Settings.Add("superTestSetting", "untouched");
                appSettings.Settings.Add("keyVaultSetting", "untouched");
                appSettings.Settings.Add("superKeyVaultSetting", "untouched");
            }

            // Run the settings through the builder
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);

            // Validation of values kind of assumes this is true. Make sure of it.
            Assert.Equal(customEndPoint, builder.Endpoint);

            // All cases are the same on the System.Configuration side of things, so all these should
            // retrieve the same value, regardless of whether that value came from AzConfig or not.
            string caseValue =  appSettings.Settings["casetestsetting"]?.Value;
            Assert.Equal(caseValue, appSettings.Settings["caseTestSetting"]?.Value);
            Assert.Equal(caseValue, appSettings.Settings["cAsEtEstsEttIng"]?.Value);

            // The key vault and case tests depend on extra parameters beyond just filters. Instead of
            // littering the giant if statement below with yet more if's, let's do a pre-check of
            // these extra conditions here. Then use null vs "untouched" values to indicate in that
            // big if statement whether we expect new values for these or not.
            bool greedy = (builder.Mode == KeyValueMode.Greedy);
            string untouched = (greedy) ? null : "untouched";
            string kvregex = (builder.UseAzureKeyVault) ? null : kvUriRegex;

            //==================================================================================================
            // Four cases here. 1) No filter, 2) Just Key filter, 3) Just Label filter, 4) Both filters.
            // In all four cases, we can expect differences between the old times and the current time.
            // When verifying count, non-greedy always has 6. Greedy will vary.

            // ---------- No Filter ----------
            if (builder.KeyFilter == null && builder.LabelFilter == null)
            {
                if (oldTimes)
                {
                    Assert.Equal(untouched ?? "altCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                    if (AzConfigHistoryInTact)
                    {
                        Assert.Equal("oldTestValue", appSettings.Settings["testSetting"]?.Value);
                        Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    }
                    else
                    {
                        Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    }
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(greedy ? (AzConfigHistoryInTact ? 5 : 2) : 6, appSettings.Settings.Count);    // "newTestSetting" is staged "in the xml" when not greedy.
                }
                else
                {
                    Assert.Equal(untouched ?? "newCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal("newTestValue", appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal("ntOGValue", appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 7 : 6, appSettings.Settings.Count);    // "newTestSetting" show up in any mode now. "epochDTO" as well in greedy.
                }
            }
            // ---------- Key Filter Only ----------
            else if (builder.KeyFilter != null && builder.LabelFilter == null)
            {
                if (oldTimes)
                {
                    Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    if (AzConfigHistoryInTact)
                        Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    else
                        Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? (AzConfigHistoryInTact ? 3 : 2) : 6, appSettings.Settings.Count);
                }
                else
                {
                    Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 3 : 6, appSettings.Settings.Count);
                }
            }
            // ---------- Label Filter Only ----------
            else if (builder.KeyFilter == null && builder.LabelFilter != null)
            {
                if (oldTimes)
                {
                    Assert.Equal(untouched ?? "altCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                    if (AzConfigHistoryInTact)
                    {
                        Assert.Equal("altTestValue", appSettings.Settings["testSetting"]?.Value);
                        Assert.Matches(kvregex ?? kva_value_new, appSettings.Settings["keyVaultSetting"]?.Value);
                    }
                    else
                    {
                        Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    }
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? (AzConfigHistoryInTact ? 3 : 1): 6, appSettings.Settings.Count);
                }
                else
                {
                    Assert.Equal(untouched ?? "altCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal("newAltValue", appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal("ntValueA", appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("newSuperAlpha", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 5 : 6, appSettings.Settings.Count);
                }
            }
            // ---------- Both Key and Label Filter ----------
            else
            {
                if (oldTimes)
                {
                    Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 1 : 6, appSettings.Settings.Count);
                }
                else
                {
                    Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("newSuperAlpha", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 2 : 6, appSettings.Settings.Count);
                }
            }
        }
    }
}
