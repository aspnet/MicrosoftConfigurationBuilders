using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Runtime;
using System.Web;
using Azure.Core;
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
        private static readonly DateTimeOffset oldTimeFilter = DateTimeOffset.Parse("April 15, 2002 9:00am");
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
                // For efficiency when debugging, we might not want to "clear out" and restage this on every run
                bool recreateTestData = true;

                if (recreateTestData)
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

                // We always need to know the epoch time, so we can compare against it.
                var epochClient = new ConfigurationClient(new Uri(customEndPoint), new DefaultAzureCredential());
                customEpoch = ((DateTimeOffset)epochClient.GetConfigurationSetting("epochDTO").Value.LastModified).AddSeconds(1);
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
        public static IEnumerable<object[]> GetCommonTestParameters()
        {
            foreach (string keyFilter in new[] { null, "", $"{CommonBuilderTests.CommonKVPrefix}*" })
                yield return new object[] { keyFilter };
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_GetValue(string keyFilter)
        {
            CommonBuilderTests.GetValue(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGetValue",
                new NameValueCollection() { { "endpoint", commonEndPoint }, { "keyFilter", keyFilter } }, caseSensitive: true);

            // The presence of a KeyFilter shortcuts GetValue() to return null under the assumption that we already
            // checked the value cache before calling GetValue(). So don't try to test KeyFilter here.
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_GetAllValues(string keyFilter)
        {
            // Keyfilter filters first on server when fetching, then the resulting set with us is again filtered by prefix.

            // Normally this common test is reflective of 'Greedy' operations. But AzureAppConfigurationBuilder sometimes
            // uses this 'GetAllValues' technique in both greedy and non-greedy modes, depending on keyFilter. So we'll test both here.

            CommonBuilderTests.GetAllValues(() => new AzureAppConfigurationBuilder(), "AzureAppConfigStrictGetAllValues",
                new NameValueCollection() { { "endpoint", commonEndPoint }, { "mode", KeyValueMode.Strict.ToString() }, { "keyFilter", keyFilter } });

            CommonBuilderTests.GetAllValues(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGreedyGetAllValues",
                new NameValueCollection() { { "endpoint", commonEndPoint }, { "mode", KeyValueMode.Greedy.ToString() }, { "keyFilter", keyFilter } });
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_ProcessConfigurationSection(string keyFilter)
        {
            // The common test will try Greedy and Strict modes.
            CommonBuilderTests.ProcessConfigurationSection(() => new AzureAppConfigurationBuilder(), "AzureAppConfigProcessConfig",
                new NameValueCollection() { { "endpoint", commonEndPoint }, { "keyFilter", keyFilter } }, caseSensitive: true);
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
            foreach (KeyValueMode mode in new[] { KeyValueMode.Strict, KeyValueMode.Greedy })
            {
                // MinValue is interpretted as "no filter" by Azure AppConfig.
                foreach (var dto in new object[] { null, DateTimeOffset.MinValue, oldTimeFilter, customEpochPlaceholder, DateTimeOffset.MaxValue })
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

            // MinValue is interpretted as "no filter" by Azure AppConfig. So only our epoch time counts as "old."
            bool? isOld = null;
            if (dtFilter == customEpoch)
                isOld = true;
            else if (dtFilter != oldTimeFilter)
                isOld = false;

            // Trying all sorts of combinations with just one test and lots of theory data
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigFilters",
                new NameValueCollection() { { "endpoint", customEndPoint }, { "mode", mode.ToString() }, { "keyFilter", keyFilter }, { "labelFilter", labelFilter },
                                            { "acceptDateTime", dtFilter.ToString() }, { "useAzureKeyVault", useAzure.ToString() }});
            ValidateExpectedConfig(builder, isOld);
        }

        // ======================================================================
        //   Errors
        // ======================================================================
        [Theory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void AzureAppConfig_ErrorsOptional(KeyValueEnabled enabled)
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

                // Unauthorized access
                exception = Record.Exception(() =>
                {
                    builder = TestHelper.CreateBuilder<BadCredentialAppConfigBuilder>(() => new BadCredentialAppConfigBuilder(), "AzureAppConfigErrors10",
                        new NameValueCollection() { { "endpoint", customEndPoint }, { "enabled", enabled.ToString() } });
                    // Azure SDK connects lazily, so the invalid credential won't be caught until we make the KV client try to use it.
                    Assert.Null(builder.GetValue("doesNotExist"));
                });
                if (enabled == KeyValueEnabled.Enabled) // We called GetValue() directly, so the exception will not be wrapped
                    TestHelper.ValidateBasicException<AuthenticationFailedException>(exception, "ClientSecretCredential authentication failed", "not-a-valid");
                else
                    Assert.Null(exception);

                // TODO: Can we do one with an unauthorized access to key-vault?
            }

            // TODO: Can we produce an invalid KeyVault reference? That should throw an error.
        }


        // ======================================================================
        //   Helpers
        // ======================================================================
        // TODO: Mock ConfigurationClient. Much work, and we'd need to inject it into the builder.
        private string GetExpectedConfigValue(AzureAppConfigurationBuilder builder, string key, bool? beforeEpoch)
        {
            // Before everything, there should be nothing
            if (beforeEpoch == null)
                return null;

            // Key filter can be figured out before the switch to keep things simple
            if (!String.IsNullOrWhiteSpace(builder.KeyFilter))
            {
                // Trim the trailing '*' if it exists
                string filter = builder.KeyFilter.TrimEnd('*');

                if (!key.StartsWith(filter))    // Case matters
                    return null;
            }

            string kvreturn = null;

            bool noLabel = String.IsNullOrWhiteSpace(builder.LabelFilter);
            bool labelA = builder.LabelFilter == "labelA";  // Case matters

            switch (key)
            {
                case "epochDTO":
                    // We don't validate the value. Just don't return null if it isn't filtered out by labels.
                    return (noLabel) ? customEpochPlaceholder.ToString() : null;
                case "caseTestSetting":
                    if (beforeEpoch.Value)
                        return (noLabel || labelA) ? "altCaseTestValue" : null;
                    return (labelA) ? "altCaseTestValue" : (noLabel) ? "newCaseTestValue" : null;
                case "testSetting":
                    if (beforeEpoch.Value)
                        return (labelA) ? "altTestValue" : (noLabel) ? "oldTestValue" : null;
                    return (labelA) ? "newAltValue" : (noLabel) ? "newTestValue" : null;
                case "newTestSetting":
                    if (beforeEpoch.Value)
                        return null;
                    return (labelA) ? "ntValueA" : (noLabel) ? "ntOGValue" : null;
                case "superTestSetting":
                    if (beforeEpoch.Value)
                        return (noLabel) ? "oldSuperValue" : null;
                    return (labelA) ? "newSuperAlpha" : (noLabel) ? "oldSuperValue" : null; // Probably null - unless label was 'labelB' which we are using in tests yet
                case "keyVaultSetting":
                    if (beforeEpoch.Value)
                        kvreturn = (labelA) ? kva_value_new : (noLabel) ? kva_value_old : null;
                    else
                        kvreturn = (labelA) ? kvb_value : (noLabel) ? kva_value_old : null;
                    break;
                case "superKeyVaultSetting":
                    kvreturn = (!noLabel) ? null : (beforeEpoch.Value) ? kvb_value : kva_value_old;
                    break;
            }

            // If KeyVault is not enabled, we'll just see a URL for config values.
            if (kvreturn != null && !builder.UseAzureKeyVault)
                return kvUriRegex;
            return kvreturn;
        }

        private void ValidateExpectedConfig(AzureAppConfigurationBuilder builder, bool? beforeEpoch)
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;

            // Prime the appSettings section as expected for this test
            appSettings.Settings.Add("casetestsetting", "untouched");   // Yes, the case is wrong. That's the point.
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

            // All the System.Configuration side of things should be case-insensitive, so all these should
            // retrieve the same value, regardless of whether that value came from AzConfig or not.
            string caseValue =  appSettings.Settings["casetestsetting"]?.Value;
            Assert.Equal(caseValue, appSettings.Settings["caseTestSetting"]?.Value);
            Assert.Equal(caseValue, appSettings.Settings["cAsEtEstsEttIng"]?.Value);

            //==================================================================================================
            if (builder.Mode == KeyValueMode.Greedy)
            {
                // 'caseTestSetting' starts off with an 'untouched' value in our test config, so it is always present.
                int expectedCount = 1;

                // We don't verify 'epochDTO', but it might get sucked in in greedy mode.
                if (GetExpectedConfigValue(builder, "epochDTO", beforeEpoch) != null)
                    expectedCount++;

                // In Greedy mode, we'll get a value for 'caseTestSetting' and then back in .Net config world, we
                // will put that in place of the already existing 'casetestsetting' value.
                var expectedValue = GetExpectedConfigValue(builder, "caseTestSetting", beforeEpoch) ?? "untouched";
                Assert.Equal(expectedValue, appSettings.Settings["caseTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "testSetting", beforeEpoch);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["testSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "newTestSetting", beforeEpoch);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["newTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "superTestSetting", beforeEpoch);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["superTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "keyVaultSetting", beforeEpoch);
                if (expectedValue != null)
                {
                    expectedCount++;
                    Assert.Matches(expectedValue, appSettings.Settings["keyVaultSetting"]?.Value);
                }
                else
                    Assert.Null(appSettings.Settings["keyVaultSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "superKeyVaultSetting", beforeEpoch);
                if (expectedValue != null)
                {
                    expectedCount++;
                    Assert.Matches(expectedValue, appSettings.Settings["superKeyVaultSetting"]?.Value);
                }
                else
                    Assert.Null(appSettings.Settings["superKeyVaultSetting"]?.Value);

                Assert.Equal(expectedCount, appSettings.Settings.Count);
            }
            else // KeyValueMode.Strict - we don't test Token. It only differs from Strict in the common base class.
            {
                // In strict mode, we ask Azure AppConfig directly for 'casetestsetting' and get nothing since the case doesn't match. So it stays as 'untouched'.
                Assert.Equal("untouched", appSettings.Settings["caseTestSetting"]?.Value);
                Assert.Equal(GetExpectedConfigValue(builder, "testSetting", beforeEpoch) ?? "untouched", appSettings.Settings["testSetting"]?.Value);
                Assert.Equal(GetExpectedConfigValue(builder, "newTestSetting", beforeEpoch) ?? "untouched", appSettings.Settings["newTestSetting"]?.Value);
                Assert.Equal(GetExpectedConfigValue(builder, "superTestSetting", beforeEpoch) ?? "untouched", appSettings.Settings["superTestSetting"]?.Value);
                Assert.Matches(GetExpectedConfigValue(builder, "keyVaultSetting", beforeEpoch) ?? "untouched", appSettings.Settings["keyVaultSetting"]?.Value);
                Assert.Matches(GetExpectedConfigValue(builder, "superKeyVaultSetting", beforeEpoch) ?? "untouched", appSettings.Settings["superKeyVaultSetting"]?.Value);

                Assert.Equal(6, appSettings.Settings.Count); // No 'epochDTO' in our staged config.
            }
        }

        private class BadCredentialAppConfigBuilder : AzureAppConfigurationBuilder
        {
            protected override TokenCredential GetCredential() => new ClientSecretCredential("not-a-valid-tenantid", "not-a-valid-clientid", "not-a-valid-clientsecret");

            protected override ConfigurationClientOptions GetConfigurationClientOptions() => base.GetConfigurationClientOptions();

            protected override SecretClientOptions GetSecretClientOptions() => base.GetSecretClientOptions();
        }
    }
}
