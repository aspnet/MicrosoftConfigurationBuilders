using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Web;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
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
        private static readonly string kva_value_old = "versionedValue-Older";
        private static readonly string kva_value_new = "versionedValue-Current";
        private static readonly string kvb_value = "mappedValue";
        private static readonly string kvUriRegex = "{\"uri\":\".+\"}";
        private static readonly string placeholderEndPoint = "https://placeholder-EndPoint.example.com";
        private static readonly string commonEndPoint;
        private static readonly string customEndPoint;
        private static readonly string keyVaultName;
        private static readonly DateTimeOffset customEpochPlaceholder = DateTimeOffset.Parse("December 31, 1999  11:59pm");
        private static readonly DateTimeOffset customEpoch;
        private static Exception StaticCtorException;


        // Update this to true to enable AzConfig tests.
        public static bool AppConfigTestsEnabled
        {
            get
            {
                // Convenience for local development. Leave this commented when committing.
                //return false;

                // If we have connection info, consider these tests enabled.
                if (String.IsNullOrWhiteSpace(commonEndPoint))
                    return false;
                if (String.IsNullOrWhiteSpace(customEndPoint))
                    return false;
                if (String.IsNullOrWhiteSpace(keyVaultName))
                    return false;
                return true;
            }
        }

        static AzureAppConfigTests()
        {
            commonEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Common");
            customEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Custom");
            keyVaultName = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Custom");

            // If tests are disabled, there is no need to do anything in here.
            if (!AppConfigTestsEnabled) { return; }

            try
            {
                // The Common config store gets filled out, but the store itself is assumed to already exist.
                ConfigurationClient cfgClient = new ConfigurationClient(new Uri(commonEndPoint), new DefaultAzureCredential());
                foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                {
                    UpdateConfigSetting(cfgClient, key, CommonBuilderTests.CommonKeyValuePairs[key]);
                }

                // The Custom config store also gets re-filled out, but the store itself is assumed to already exist.
                // Leverage the custom key vault used in the KV config builder tests.
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

                // First, ensure the KeyVault values are populated in Key Vault
                SecretClient kvClient = new SecretClient(new Uri($"https://{keyVaultName}.vault.azure.net"), new DefaultAzureCredential());
                var kvb_cur = AzureTests.EnsureCurrentSecret(kvClient, "mapped-test-key", "mappedValue");
                var kva_old = AzureTests.EnsureActiveSecret(kvClient, "versioned-key", "versionedValue-Older");
                var kva_cur = AzureTests.EnsureCurrentSecret(kvClient, "versioned-key", "versionedValue-Current");

                // Now re-create the config settings
                cfgClient = new ConfigurationClient(new Uri(customEndPoint), new DefaultAzureCredential());

                // Start by clearing all the setting to get a fresh look. (Incidentally, I think clearing these values only does so
                // at this point in time. Meaning if you set a SettingSelector with a timestamp from just before this, you'll still
                // see the old, dirty value. Which makes establishing this "clean" point with no values before our epoch time
                // all the more important.)
                ClearConfigSettings(cfgClient);

                // First create config settings with timestamps before the epoch
                UpdateConfigSetting(cfgClient, "testSetting", "oldTestValue");
                UpdateConfigSetting(cfgClient, "superTestSetting", "oldSuperValue");
                UpdateConfigSecret(cfgClient, "keyVaultSetting", kva_old);
                UpdateConfigSecret(cfgClient, "superKeyVaultSetting", kvb_cur);

                UpdateConfigSetting(cfgClient, "caseTestSetting", "altCaseTestValue", "labelA");
                UpdateConfigSetting(cfgClient, "testSetting", "altTestValue", "labelA");
                UpdateConfigSecret(cfgClient, "keyVaultSetting", kva_cur, "labelA");

                // Remember the epoch. Don't take time directly, as machine time might be off from server time.
                UpdateConfigSetting(cfgClient, "epochDTO", "useTimeStampOfThisSetting-" + DateTime.Now.Ticks);
                System.Threading.Thread.Sleep(3000);
                customEpoch = ((DateTimeOffset)cfgClient.GetConfigurationSetting("epochDTO").Value.LastModified).AddSeconds(1);

                // Then ensure/create config settins after the epoch
                UpdateConfigSetting(cfgClient, "caseTestSetting", "newCaseTestValue");
                UpdateConfigSetting(cfgClient, "testSetting", "newTestValue");
                UpdateConfigSetting(cfgClient, "newTestSetting", "ntOGValue");
                UpdateConfigSecret(cfgClient, "keyVaultSetting", kva_old);
                UpdateConfigSecret(cfgClient, "superKeyVaultSetting", kva_old);

                UpdateConfigSetting(cfgClient, "testSetting", "newAltValue", "labelA");
                UpdateConfigSetting(cfgClient, "newTestSetting", "ntValueA", "labelA");
                UpdateConfigSetting(cfgClient, "superTestSetting", "newSuperAlpha", "labelA");
                UpdateConfigSecret(cfgClient, "keyVaultSetting", kvb_cur, "labelA");

                UpdateConfigSetting(cfgClient, "superTestSetting", "newSuperBeta", "labelB");
                UpdateConfigSecret(cfgClient, "superKeyVaultSetting", kva_cur, "labelB");
            }
            catch (Exception ex)
            {
                StaticCtorException = ex;
            }
        }
        public AzureAppConfigTests()
        {
            // Errors in the static constructor get swallowed by the testrunner. :(
            // But this hacky method will bubble up any exceptions we encounter there.
            if (StaticCtorException != null)
                throw new Exception("Static ctor encountered an exception:", StaticCtorException);
        }

        static ConfigurationSetting UpdateConfigSetting(ConfigurationClient client, string key, string value, string label = null)
            => client.SetConfigurationSetting(new ConfigurationSetting(key, value, label), false);  // Overwrite without exception
        static SecretReferenceConfigurationSetting UpdateConfigSecret(ConfigurationClient client, string key, KeyVaultSecret secret, string label = null)
            => (SecretReferenceConfigurationSetting)client.SetConfigurationSetting(new SecretReferenceConfigurationSetting(key, secret.Id, label), false);
        static void ClearConfigSettings(ConfigurationClient client)
        {
            SettingSelector selector = new SettingSelector() { KeyFilter = "*", LabelFilter = "*" };
            foreach (ConfigurationSetting rev in client.GetRevisions(selector))
            {
                client.DeleteConfigurationSetting(rev);
            }
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
        [Fact]
        public void AzureAppConfig_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigDefault",
                new NameValueCollection() { { "endpoint", placeholderEndPoint } });

            // Endpoint matches, ConnectionString is null
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
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


        [Fact]
        public void AzureAppConfig_Settings()
        {
            // Endpoint is case insensitive, connection string is null
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings1",
                new NameValueCollection() { { "EndpOInt", placeholderEndPoint } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
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
                new NameValueCollection() { { "connectionString", fakeCS }, { "endpoint", placeholderEndPoint } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);

            // These tests require executing the builder, which needs a valid endpoint.
            if (AppConfigTestsEnabled)
            {
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
        }

        public static IEnumerable<object[]> GetFilterTestParameters()
        {
            // xUnit evaluates MemberData before ever constructing this class. So we can't use customEpoch or any class variable here.
            foreach (KeyValueMode mode in new [] { KeyValueMode.Strict, KeyValueMode.Greedy })
            {
                foreach (var dto in new object[] { null, DateTimeOffset.MinValue, customEpochPlaceholder, DateTimeOffset.MaxValue })
                {
                    yield return new object[] { mode, null, null, dto, false };
                    yield return new object[] { mode, "", "", dto, true };
                    yield return new object[] { mode, "super*", null, dto, true };
                    yield return new object[] { mode, null, "labelA", dto, true };
                    yield return new object[] { mode, "super*", "labelA", dto, false };
                    yield return new object[] { mode, "Super*", null, dto, false }; // Case sensitive key filter
                    yield return new object[] { mode, null, "labela", dto, false }; // Case sensitive label filter
                }
            }
        }

        [AppConfigTheory]
        [MemberData(nameof(GetFilterTestParameters))]
        public void AzureAppConfig_Filters(KeyValueMode mode, string keyFilter, string labelFilter, DateTimeOffset dtFilter, bool useAzure)
        {
            // xUnit evaluates MemberData before ever constructing this class. Make sure we're using the correct epoch time.
            if (dtFilter == customEpochPlaceholder)
                dtFilter = customEpoch;

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
        [Theory]
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
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "labelFilter", "invalid*" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors4", "not supported in label filters");
            else
                Assert.Null(exception);
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors5",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "labelFilter", "invalid,comma" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors5", "not supported in label filters");
            else
                Assert.Null(exception);

            // Invalid acceptDateTime (Not a DateTimeOffset)
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors6",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "acceptDateTime", "neither a date nor time" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<FormatException>(exception, "AzureAppConfigErrors6");
            else
                Assert.Null(exception);

            // Invalid useAzureKeyVault
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors7",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "useAzureKeyVault", "neither tru nor fals" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<FormatException>(exception, "AzureAppConfigErrors7");
            else
                Assert.Null(exception);

            // These tests require executing the builder, which needs a valid endpoint.
            if (AppConfigTestsEnabled)
            {
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
            }

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
                    Assert.Equal("oldTestValue", appSettings.Settings["testSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    // "newTestSetting" is only in the pre-built config for non-greedy. "epochDTO" isn't ever, but gets sucked in when greedy. In the end, they cancel out.
                    Assert.Equal(6, appSettings.Settings.Count);
                }
                else
                {
                    Assert.Equal(untouched ?? "newCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal("newTestValue", appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal("ntOGValue", appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["keyVaultSetting"]?.Value);
                    Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    Assert.Equal(greedy ? 7 : 6, appSettings.Settings.Count);    // "newTestSetting" now shows up in any mode. (+epochDTO in greedy)
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
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    if (builder.KeyFilter == builder.KeyFilter.ToLower())
                    {
                        // Key filter is case-sensitive: 'super*' will match keys in AppConfig...
                        Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["superKeyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 3 : 6, appSettings.Settings.Count);
                    }
                    else
                    {
                        // ... but 'SUPER*' will not match any keys in AppConfig.
                        // (Values retrieved from App Config are applied to .Net config case-insensitively, but that's another testcase.)
                        Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 1 : 6, appSettings.Settings.Count);
                    }
                }
                else
                {
                    Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                    if (builder.KeyFilter == builder.KeyFilter.ToLower())
                    {
                        // Key filter is case-sensitive: 'super*' will match keys in AppConfig...
                        Assert.Equal("oldSuperValue", appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Matches(kvregex ?? kva_value_old, appSettings.Settings["superKeyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 3 : 6, appSettings.Settings.Count);
                    }
                    else
                    {
                        // ... but 'SUPER*' will not match any keys in AppConfig.
                        // (Values retrieved from App Config are applied to .Net config case-insensitively, but that's another testcase.)
                        Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 1 : 6, appSettings.Settings.Count);
                    }
                }
            }
            // ---------- Label Filter Only ----------
            else if (builder.KeyFilter == null && builder.LabelFilter != null)
            {
                if (oldTimes)
                {
                    Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    if (builder.LabelFilter != builder.LabelFilter.ToLower())
                    {
                        // Label filter is case-sensitive: 'labelA' will match labels in AppConfig...
                        Assert.Equal(untouched ?? "altCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                        Assert.Equal("altTestValue", appSettings.Settings["testSetting"]?.Value);
                        Assert.Matches(kvregex ?? kva_value_new, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 3 : 6, appSettings.Settings.Count);
                    }
                    else
                    {
                        // ... but 'labela' will not match any labels in AppConfig.
                        Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 1 : 6, appSettings.Settings.Count);
                    }
                }
                else
                {
                    Assert.Equal(untouched, appSettings.Settings["superKeyVaultSetting"]?.Value);
                    if (builder.LabelFilter != builder.LabelFilter.ToLower())
                    {
                        // Label filter is case-sensitive: 'labelA' will match labels in AppConfig...
                        Assert.Equal(untouched ?? "altCaseTestValue", appSettings.Settings["casetestsetting"]?.Value);
                        Assert.Equal("newAltValue", appSettings.Settings["testSetting"]?.Value);
                        Assert.Equal("ntValueA", appSettings.Settings["newTestSetting"]?.Value);
                        Assert.Equal("newSuperAlpha", appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Matches(kvregex ?? kvb_value, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 5 : 6, appSettings.Settings.Count);
                    }
                    else
                    {
                        // ... but 'labela' will not match any labels in AppConfig.
                        Assert.Equal("untouched", appSettings.Settings["casetestsetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["testSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["newTestSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["superTestSetting"]?.Value);
                        Assert.Equal(untouched, appSettings.Settings["keyVaultSetting"]?.Value);
                        Assert.Equal(greedy ? 1 : 6, appSettings.Settings.Count);
                    }
                }
            }
            // ---------- Both Key and Label Filter ----------
            else
            {
                // Key and Label filters are case-sensitive... but they are tested independently of each other.
                // If providing testcase scenarios where both filters bump against case-sensitivity, then this
                // section will need to be updated accordingly.
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
