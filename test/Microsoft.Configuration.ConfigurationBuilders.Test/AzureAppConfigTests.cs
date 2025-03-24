using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public static class AppConfigConstants
    {
        /* Convenience to keep full-stack out of the way during local development. Leave 'false' when committing.  */
        public static readonly bool DisableFullStackTests = false;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AppConfigFactAttribute : FactAttribute
    {
        public AppConfigFactAttribute(string Reason = null)
        {
            if (!AppConfigFixture.FullStackTestsEnabled)
                Skip = Reason ?? "Skipped: Azure AppConfig Tests Disabled.";
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class AppConfigTheoryAttribute : TheoryAttribute
    {
        public AppConfigTheoryAttribute(string Reason = null)
        {
            if (!AppConfigFixture.FullStackTestsEnabled)
                Skip = Reason ?? "Skipped: Azure AppConfig Tests Disabled.";
        }
    }

    enum ConfigAge
    {
        IsAncient,
        IsOld,
        IsNew,
        IsSnapshot,
    };

    public class AppConfigFixture : IDisposable
    {
        /* Convenience to keep full-stack out of the way during local development. Leave 'true' when committing.  */
        public static readonly bool FullStackTestsEnabled;
        public static readonly string CommonEndPoint;
        public static readonly string CustomEndPoint;
        public static readonly string KeyVaultName;

        public DateTimeOffset CustomEpoch { get; private set; }
        public string TestSnapName { get; private set; }
        public string KVA_Value_Old { get; private set; } = "versionedValue-Older";
        public string KVA_Value_New { get; private set; } = "versionedValue-Current";
        public string KVB_Value { get; private set; } = "mappedValue";


        static AppConfigFixture()
        {
            CommonEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Common");
            CustomEndPoint = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AzConfig.Custom");
            KeyVaultName = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Custom");

            FullStackTestsEnabled = !AppConfigConstants.DisableFullStackTests && !String.IsNullOrWhiteSpace(CommonEndPoint)
                && !String.IsNullOrWhiteSpace(CustomEndPoint) && !String.IsNullOrWhiteSpace(KeyVaultName);
        }

        public AppConfigFixture()
        {
            // If full-stack tests are disabled, there is no need to do anything in here.
            if (!FullStackTestsEnabled) { return; }

            // The Common config store gets filled out, but the store itself is assumed to already exist.
            ConfigurationClient cfgClient = new ConfigurationClient(new Uri(CommonEndPoint), new DefaultAzureCredential());
            var currentCommonSettings = GetAllConfigSettings(cfgClient);
            var recreateCommonSettings = currentCommonSettings.Count != CommonBuilderTests.CommonKeyValuePairs.Count;
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                if (!currentCommonSettings.Exists(s => s.Key == key && s.Value == CommonBuilderTests.CommonKeyValuePairs[key]))
                    recreateCommonSettings = true;
            }
            if (recreateCommonSettings)
            {
                ClearConfigSettings(cfgClient);
                foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
                {
                    UpdateConfigSetting(cfgClient, key, CommonBuilderTests.CommonKeyValuePairs[key]);
                }
            }

            // The Custom config store also gets re-filled out, but the store itself is assumed to already exist.
            // Leverage the custom key vault used in the KV config builder tests.
            //
            //      kva:    versioned-key == versionedValue-Current
            //                               versionedValue-Older
            //      kvb:    mapped-test-key == mappedValue

            // Time -->                 Beginning       (labelA)            |epoch|               (labelA)        (labelB)
            // epochDTO                                                     DateTimeOffset-of-the-epoch (show up after epoch)
            // caseTestSetting                          altCaseTestValue    newCaseTestValue
            // testSetting              oldTestValue    altTestValue        newTestValue    newAltValue
            // newTestSetting                                               ntOGValue       ntValueA
            // superTestSetting         oldSuperValue                                       newSuperAlpha   newSuperBeta
            // keyVaultSetting          kva_value_old   kva_value_new       kva_value_old   kvb_value
            // superKeyVaultSetting     kvb_value                           kva_value_old                   kva_value_new
            //
            // Snapshots - defined by 1-3 filters. Order determines precedence, as any key can only have one value in a
            //      snapshot - meaning if filters with labelA and labelB are defined, the value for 'superTestSetting'
            //      will ultimately be determined by which filter is primary.
            //
            // Define a snapshot with:
            //      filter 1: superKeyVaultSetting + labelB
            //      filter 2: testSetting
            //      filter 3: labelA
            //

            // First, ensure the KeyVault values are populated in Key Vault
            SecretClient kvClient = new SecretClient(new Uri($"https://{KeyVaultName}.vault.azure.net"), new DefaultAzureCredential());
            var kvb_cur = AzureFixture.EnsureCurrentSecret(kvClient, "mapped-test-key", KVB_Value);
            var kva_old = AzureFixture.EnsureActiveSecret(kvClient, "versioned-key", KVA_Value_Old);
            var kva_cur = AzureFixture.EnsureCurrentSecret(kvClient, "versioned-key", KVA_Value_New);

            // Now check the custom AppConfig store - starting with the epoch time.
            cfgClient = new ConfigurationClient(new Uri(CustomEndPoint), new DefaultAzureCredential());
            var epochSetting = cfgClient.GetConfigurationSetting("epochDTO");
            bool recreateCustomData = !(epochSetting?.Value?.LastModified > DateTimeOffset.MinValue);

            // Next, check that the data is correct at the epoch.
            if (!recreateCustomData)
            {
                var initialCustomSettings = GetAllConfigSettings(cfgClient, epochSetting.Value.LastModified);

                // These config values should exist...
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "epochDTO" && string.IsNullOrEmpty(s.Label));
                recreateCustomData |= !initialCustomSettings.Find(s => s.Key == "epochDTO" && string.IsNullOrEmpty(s.Label)).Value.StartsWith("useTimeStampOfThisSetting-");
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "testSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "oldTestValue");
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "superTestSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "oldSuperValue");
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "keyVaultSetting" && string.IsNullOrEmpty(s.Label) && IsSecret(s, kva_old));
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "superKeyVaultSetting" && string.IsNullOrEmpty(s.Label) && IsSecret(s, kvb_cur));

                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "caseTestSetting" && s.Label == "labelA" && s.Value == "altCaseTestValue");
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "testSetting" && s.Label == "labelA" && s.Value == "altTestValue");
                recreateCustomData |= !initialCustomSettings.Exists(s => s.Key == "keyVaultSetting" && s.Label == "labelA" && IsSecret(s, kva_cur));

                // And these should not...
                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "caseTestSetting" && string.IsNullOrEmpty(s.Label));
                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "newTestSetting" && string.IsNullOrEmpty(s.Label));

                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "epochDTO" && s.Label == "labelA");
                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "newTestSetting" && s.Label == "labelA");
                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "superTestSetting" && s.Label == "labelA");
                recreateCustomData |= initialCustomSettings.Exists(s => s.Key == "superKeyVaultSetting" && s.Label == "labelA");

                recreateCustomData |= initialCustomSettings.Exists(s => s.Label == "labelB");
            }

            // After that, check the data after the epoch.
            if (!recreateCustomData)
            {
                var currentCustomSettings = GetAllConfigSettings(cfgClient);

                // These config values should exist...
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "epochDTO" && string.IsNullOrEmpty(s.Label));
                recreateCustomData |= !currentCustomSettings.Find(s => s.Key == "epochDTO" && string.IsNullOrEmpty(s.Label)).Value.StartsWith("useTimeStampOfThisSetting-");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "caseTestSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "newCaseTestValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "testSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "newTestValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "newTestSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "ntOGValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "superTestSetting" && string.IsNullOrEmpty(s.Label) && s.Value == "oldSuperValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "keyVaultSetting" && string.IsNullOrEmpty(s.Label) && IsSecret(s, kva_old));
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "superKeyVaultSetting" && string.IsNullOrEmpty(s.Label) && IsSecret(s, kva_old));

                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "caseTestSetting" && s.Label == "labelA" && s.Value == "altCaseTestValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "testSetting" && s.Label == "labelA" && s.Value == "newAltValue");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "newTestSetting" && s.Label == "labelA" && s.Value == "ntValueA");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "superTestSetting" && s.Label == "labelA" && s.Value == "newSuperAlpha");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "keyVaultSetting" && s.Label == "labelA" && IsSecret(s, kvb_cur));

                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "superTestSetting" && s.Label == "labelB" && s.Value == "newSuperBeta");
                recreateCustomData |= !currentCustomSettings.Exists(s => s.Key == "superKeyVaultSetting" && s.Label == "labelB" && IsSecret(s, kva_cur));

                // And these should not...
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "epochDTO" && s.Label == "labelA");
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "superKeyVaultSetting" && s.Label == "labelA");

                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "epochDTO" && s.Label == "labelB");
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "caseTestSetting" && s.Label == "labelB");
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "testSetting" && s.Label == "labelB");
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "newTestSetting" && s.Label == "labelB");
                recreateCustomData |= currentCustomSettings.Exists(s => s.Key == "keyVaultSetting" && s.Label == "labelB");
            }

            // Now, recreate all that custom data... ONLY if we have to
            if (recreateCustomData)
            {
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

            // We always need to know the epoch time, so we can compare against it - and it should always exist at this point.
            // Grab it from the config setting timestamp though, in case of machine time skew.
            epochSetting = (recreateCustomData) ? cfgClient.GetConfigurationSetting("epochDTO") : epochSetting;
            CustomEpoch = ((DateTimeOffset)epochSetting.Value.LastModified).AddSeconds(1);

            // Finally, setup a test snapshot if one doesn't already exist. (Use epoch-based name for verification.)
            TestSnapName = "testSnapshot" + epochSetting.Value.Value.Substring(epochSetting.Value.Value.IndexOf('-'));
            if (cfgClient.GetSnapshots(new SnapshotSelector() { NameFilter = TestSnapName }).Count() <= 0)
            {
                var filters = new List<ConfigurationSettingsFilter> {
                        // Priority order is reverse of this list for some reason. :/
                        new ConfigurationSettingsFilter("") { Label = "labelA" },
                        new ConfigurationSettingsFilter("testSetting"),
                        new ConfigurationSettingsFilter("superKeyVaultSetting") { Label = "labelB" },
                    };
                var testSnapshot = new ConfigurationSnapshot(filters);
                var operation = cfgClient.CreateSnapshot(WaitUntil.Completed, TestSnapName, testSnapshot);
                if (!operation.HasValue)
                    throw new Exception("Creation of test snapshot for AppConfig failed.");
                cfgClient.ArchiveSnapshot(TestSnapName);
            }
        }

        static List<ConfigurationSetting> GetAllConfigSettings(ConfigurationClient client, DateTimeOffset? asOfTime = null)
        {
            List<ConfigurationSetting> allSettings = new List<ConfigurationSetting>();
            foreach (ConfigurationSetting s in client.GetConfigurationSettings(new SettingSelector() { KeyFilter = "*", LabelFilter = "*", AcceptDateTime = asOfTime }))
            {
                allSettings.Add(s);
            }
            return allSettings;
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
        static bool IsSecret(ConfigurationSetting setting, KeyVaultSecret secret)
            => setting is SecretReferenceConfigurationSetting && ((SecretReferenceConfigurationSetting)setting).SecretId == secret.Id;

        public void Dispose() { }
    }

    public class AzureAppConfigTests : IClassFixture<AppConfigFixture>
    {
        private static readonly string kvUriRegex = "{\"uri\":\".+\"}";
        private static readonly string placeholderEndPoint = "https://placeholder-EndPoint.example.com";
        private static readonly DateTimeOffset customEpochPlaceholder = DateTimeOffset.Parse("December 31, 1999  11:59pm");
        private static readonly DateTimeOffset oldTimeFilter = DateTimeOffset.Parse("April 15, 2002 9:00am");

        private readonly AppConfigFixture _fixture;

        public AzureAppConfigTests(AppConfigFixture fixture)
        {
            _fixture = fixture;
        }


        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        public static IEnumerable<object[]> GetCommonTestParameters()
        {
            // Default parameters
            yield return new[] { new NameValueCollection() { } };

            // KeyFilter
            foreach (string keyFilter in new[] { null, "", $"{CommonBuilderTests.CommonKVPrefix}*" })
                yield return new[] { new NameValueCollection { { "keyFilter", keyFilter } } };

            // Preload
            foreach (string preload in new[] { "true", "false" })
                yield return new[] { new NameValueCollection { { "preloadValues", preload } } };
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_GetValue(NameValueCollection parameters)
        {
            // The presence of a KeyFilter shortcuts GetValue() to return null under the assumption that we already
            // checked the value cache before calling GetValue(). So don't try to test KeyFilter here.
            // ProcessConfigurationSection should be able to tackle that scenario in more of a "full-stack" manner.
            CommonBuilderTests.GetValue(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGetValue",
                new NameValueCollection(parameters) { { "endpoint", AppConfigFixture.CommonEndPoint } }, caseSensitive: true);
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_GetAllValues(NameValueCollection parameters)
        {
            // Normally this common test is reflective of 'Greedy' operations. But AzureAppConfigurationBuilder sometimes
            // uses this 'GetAllValues' technique in both greedy and non-greedy modes, depending on keyFilter. So we'll test both here.

            CommonBuilderTests.GetAllValues(() => new AzureAppConfigurationBuilder(), "AzureAppConfigStrictGetAllValues",
                new NameValueCollection(parameters) { { "endpoint", AppConfigFixture.CommonEndPoint }, { "mode", KeyValueMode.Strict.ToString() } });

            CommonBuilderTests.GetAllValues(() => new AzureAppConfigurationBuilder(), "AzureAppConfigGreedyGetAllValues",
                new NameValueCollection(parameters) { { "endpoint", AppConfigFixture.CommonEndPoint }, { "mode", KeyValueMode.Greedy.ToString() } });
        }

        [AppConfigTheory]
        [MemberData(nameof(GetCommonTestParameters))]
        public void AzureAppConfig_ProcessConfigurationSection(NameValueCollection parameters)
        {
            // The common test will try Greedy and Strict modes.
            CommonBuilderTests.ProcessConfigurationSection(() => new AzureAppConfigurationBuilder(), "AzureAppConfigProcessConfig",
                new NameValueCollection(parameters) { { "endpoint", AppConfigFixture.CommonEndPoint } });
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

            // Snapshot
            Assert.Null(builder.Snapshot);

            // KeyFilter
            Assert.Null(builder.KeyFilter);

            // LabelFilter
            Assert.Null(builder.LabelFilter);

            // DateTimeFilter
            Assert.Equal(DateTimeOffset.MinValue, builder.AcceptDateTime);

            // UseAzureKeyVault
            Assert.False(builder.UseAzureKeyVault);

            // PreloadValues
            Assert.True(builder.PreloadValues);

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

            // KeyFilter, LabelFilter, and DateTimeFilter - Use 'Greedy' since KeyFilter+NotGreedy == preload all values on init
            var dts = DateTimeOffset.Now.ToString("O");  // This will get ToString() and back, losing some tick-granularity. So lets make the string our source of truth.
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings4",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "mode", KeyValueMode.Greedy.ToString() }, { "keyFilter", "fOo" },
                    { "labelFilter", "baR" }, { "acceptDateTime", dts } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.Equal("fOo", builder.KeyFilter);
            Assert.Equal("baR", builder.LabelFilter);
            Assert.Equal(DateTimeOffset.Parse(dts), builder.AcceptDateTime);

            // Snapshot - Use 'Greedy' since Snapshot+NotGreedy == preload all values on init
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings5",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "mode", KeyValueMode.Greedy.ToString() }, { "snapshot", "name_of_snapshot" }, });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.Equal("name_of_snapshot", builder.Snapshot);

            // Snapshot takes precedence over other filters
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings6",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "mode", KeyValueMode.Greedy.ToString() }, { "snapshot", "snapname" },
                    { "keyFilter", "FooBar" }, { "labelFilter", "Baz" }, { "acceptDateTime", dts }, { "useAzureKeyVault", "true" } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.Equal("snapname", builder.Snapshot);
            Assert.Null(builder.KeyFilter);
            Assert.Null(builder.LabelFilter);
            Assert.Equal(DateTimeOffset.MinValue, builder.AcceptDateTime);
            Assert.True(builder.UseAzureKeyVault);

            // PreloadValues - case insensitive
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings6_1",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "preLOADvalues", "FALSe" } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.False(builder.PreloadValues);

            // PreloadValues - triggered by keyFilter
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings6_2",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "keyFilter", "somefilter" } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.True(builder.PreloadValues);

            // PreloadValues - triggered by snapshot
            builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings6_3",
                new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "snapshot", "somename" } });
            Assert.Equal(placeholderEndPoint, builder.Endpoint);
            Assert.Null(builder.ConnectionString);
            Assert.True(builder.PreloadValues);

            // These tests require executing the builder, which needs a valid endpoint.
            if (AppConfigFixture.FullStackTestsEnabled)
            {
                // UseKeyVault is case insensitive, allows reading KeyVault values
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings7",
                new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "UsEaZuReKeYvAuLt", "tRUe" } });
                Assert.Equal(AppConfigFixture.CustomEndPoint, builder.Endpoint);
                Assert.True(builder.UseAzureKeyVault);
                Assert.Equal(_fixture.KVA_Value_Old, builder.GetValue("keyVaultSetting"));
                var allValues = builder.GetAllValues("");
                Assert.Equal(_fixture.KVA_Value_Old, TestHelper.GetValueFromCollection(allValues, "superKeyVaultSetting"));

                // UseKeyVault is case insensitive, does not allow reading KeyVault values
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigSettings8",
                    new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "useAZurekeYVaulT", "faLsE" } });
                Assert.Equal(AppConfigFixture.CustomEndPoint, builder.Endpoint);
                Assert.False(builder.UseAzureKeyVault);
                Assert.Matches(kvUriRegex, builder.GetValue("keyVaultSetting"));    // Don't care what the uri is... just that is it a URI instead of a value
                allValues = builder.GetAllValues("");
                Assert.Matches(kvUriRegex, TestHelper.GetValueFromCollection(allValues, "superKeyVaultSetting"));

                // PreloadValues - case insensitive
                var preloadBuilder = TestHelper.CreateBuilder<PreloadCheckAppConfigBuilder>(() => new PreloadCheckAppConfigBuilder(), "AzureAppConfigSettings9",
                    new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint } });
                Assert.Equal(AppConfigFixture.CustomEndPoint, builder.Endpoint);
                Assert.True(preloadBuilder.PreloadValues);
                Assert.False(preloadBuilder.CalledGetAllValues);
                preloadBuilder.GetValue("notImportant");
                Assert.True(preloadBuilder.CalledGetAllValues);

                preloadBuilder = TestHelper.CreateBuilder<PreloadCheckAppConfigBuilder>(() => new PreloadCheckAppConfigBuilder(), "AzureAppConfigSettings_10",
                    new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "preloadValues", "false" } });
                Assert.Equal(AppConfigFixture.CustomEndPoint, builder.Endpoint);
                Assert.False(preloadBuilder.PreloadValues);
                Assert.False(preloadBuilder.CalledGetAllValues);
                preloadBuilder.GetValue("notImportant");
                Assert.False(preloadBuilder.CalledGetAllValues);
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
                dtFilter = _fixture.CustomEpoch;

            // MinValue is interpretted as "no filter" by Azure AppConfig. So only our epoch time counts as "old."
            ConfigAge age = ConfigAge.IsNew;
            if (dtFilter == _fixture.CustomEpoch)
                age = ConfigAge.IsOld;
            else if (dtFilter < _fixture.CustomEpoch && dtFilter != DateTimeOffset.MinValue)
                age = ConfigAge.IsAncient;

            // Trying all sorts of combinations with just one test and lots of theory data
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigFilters",
                new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "mode", mode.ToString() }, { "keyFilter", keyFilter }, { "labelFilter", labelFilter },
                                            { "acceptDateTime", dtFilter.ToString() }, { "useAzureKeyVault", useAzure.ToString() }});
            ValidateExpectedConfig(builder, age);
        }

        [AppConfigTheory]
        [InlineData(KeyValueMode.Strict)]
        [InlineData(KeyValueMode.Greedy)]
        public void AzureAppConfig_Snapshot(KeyValueMode mode)
        {
            // Trying all sorts of combinations with just one test and lots of theory data
            var builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigFilters",
                new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "mode", mode.ToString() }, { "snapshot", _fixture.TestSnapName }, { "useAzureKeyVault", "true" } });

            ValidateExpectedConfig(builder, ConfigAge.IsSnapshot);
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

            // Invalid preloadValues
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors7_1",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "preloadValues", "neither tru nor fals" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<FormatException>(exception, "AzureAppConfigErrors7_1");
            else
                Assert.Null(exception);

            // KeyFilter requires preload
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors7_2",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "preloadValues", "false" }, { "keyFilter", "somefilter" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors7_2");
            else
                Assert.Null(exception);

            // Snapshot requires preload
            exception = Record.Exception(() =>
            {
                builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors7_3",
                    new NameValueCollection() { { "endpoint", placeholderEndPoint }, { "preloadValues", "false" }, { "snapshot", "somesnapshot" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureAppConfigErrors7_3");
            else
                Assert.Null(exception);

            // These tests require executing the builder, which needs a valid endpoint.
            if (AppConfigFixture.FullStackTestsEnabled)
            {
                // Invalid key name (can't be '.', '..', or contain '%'... not that we care, but we should handle the error gracefully)
                // Oddly, AppConfig doesn't return an error here. Just quietly returns nothing. So expect no exception.
                exception = Record.Exception(() =>
                {
                    builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors8",
                        new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "enabled", enabled.ToString() } });
                    Assert.Null(builder.GetValue("bad%keyname"));
                });
                Assert.Null(exception);

                // Invalid key character with key filter (use of key filter should trigger greedy-style pre-load even in strict mode)
                // Oddly again, AppConfig doesn't return an error here. Just quietly returns nothing. So expect no exception.
                exception = Record.Exception(() =>
                {
                    builder = TestHelper.CreateBuilder<AzureAppConfigurationBuilder>(() => new AzureAppConfigurationBuilder(), "AzureAppConfigErrors9",
                        new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "keyFilter", "bad%Filter*" }, { "enabled", enabled.ToString() } });
                    Assert.Null(builder.GetValue("bad%FilterFetchesThisValue"));
                });
                Assert.Null(exception);

                // Unauthorized access
                exception = Record.Exception(() =>
                {
                    builder = TestHelper.CreateBuilder<BadCredentialAppConfigBuilder>(() => new BadCredentialAppConfigBuilder(), "AzureAppConfigErrors10",
                        new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "enabled", enabled.ToString() } });
                    // Azure SDK connects lazily, so the invalid credential won't be caught until we make the KV client try to use it.
                    Assert.Null(builder.GetValue("doesNotExist"));
                });
                if (enabled == KeyValueEnabled.Enabled) // We called GetValue() directly, so the exception will not be wrapped
                    TestHelper.ValidateBasicException<AuthenticationFailedException>(exception, "ClientSecretCredential authentication failed", "not-a-valid");
                else
                    Assert.Null(exception);

                // Throttling should produce an exception
                // Causing this requires GetValue(), but builder won'te ever call GetValue() when disabled.
                if (enabled != KeyValueEnabled.Disabled)
                {
                    exception = Record.Exception(() =>
                    {
                        // Create a subclass that injects a transport returning HTTP 429
                        builder = TestHelper.CreateBuilder<ThrottleTestAppConfigBuilder>(() => new ThrottleTestAppConfigBuilder(), "AzureAppConfigThrottleTest",
                            new NameValueCollection() { { "endpoint", AppConfigFixture.CustomEndPoint }, { "enabled", KeyValueEnabled.Enabled.ToString() } });
                        builder.GetValue("anyKey");
                    });
                    TestHelper.ValidateBasicException<RequestFailedException>(exception, "Service request failed.", "Too many requests");
                }

                // TODO: Can we do one with an unauthorized access to key-vault?
            }

            // TODO: Can we produce an invalid KeyVault reference? That should throw an error.
        }


        // ======================================================================
        //   Helpers
        // ======================================================================

        private AppSettingsSection GetCustomAppSettings(bool addAllSettings)
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;

            // Prime the appSettings section as expected for this test
            appSettings.Settings.Add("casetestsetting", "untouched");   // Yes, the case is wrong. That's the point.
            if (addAllSettings)
            {
                appSettings.Settings.Add("testSetting", "untouched");
                appSettings.Settings.Add("newTestSetting", "untouched");
                appSettings.Settings.Add("superTestSetting", "untouched");
                appSettings.Settings.Add("keyVaultSetting", "untouched");
                appSettings.Settings.Add("superKeyVaultSetting", "untouched");
            }

            return appSettings;
        }

        // TODO: Mock ConfigurationClient. Much work, and we'd need to inject it into the builder.
        private string GetExpectedConfigValue(AzureAppConfigurationBuilder builder, string key, ConfigAge age)
        {
            // Before everything, there should be nothing
            if (age == ConfigAge.IsAncient)
                return null;

            // Snapshots are straight forward and don't bring in other filters
            if (age == ConfigAge.IsSnapshot)
            {
                switch (key)
                {
                    case "caseTestSetting":
                        return "altCaseTestValue";
                    case "testSetting":
                        return "newTestValue";
                    case "newTestSetting":
                        return "ntValueA";
                    case "superTestSetting":
                        return "newSuperAlpha";
                    case "keyVaultSetting":
                        return (builder.UseAzureKeyVault) ? _fixture.KVB_Value : kvUriRegex;
                    case "superKeyVaultSetting":
                        return (builder.UseAzureKeyVault) ? _fixture.KVA_Value_New : kvUriRegex;
                }

                return null;    // Includes epochDTO
            }

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
                    if (age == ConfigAge.IsOld)
                        return (noLabel || labelA) ? "altCaseTestValue" : null;
                    return (labelA) ? "altCaseTestValue" : (noLabel) ? "newCaseTestValue" : null;
                case "testSetting":
                    if (age == ConfigAge.IsOld)
                        return (labelA) ? "altTestValue" : (noLabel) ? "oldTestValue" : null;
                    return (labelA) ? "newAltValue" : (noLabel) ? "newTestValue" : null;
                case "newTestSetting":
                    if (age == ConfigAge.IsOld)
                        return null;
                    return (labelA) ? "ntValueA" : (noLabel) ? "ntOGValue" : null;
                case "superTestSetting":
                    if (age == ConfigAge.IsOld)
                        return (noLabel) ? "oldSuperValue" : null;
                    return (labelA) ? "newSuperAlpha" : (noLabel) ? "oldSuperValue" : null; // Probably null - unless label was 'labelB' which we are using in tests yet
                case "keyVaultSetting":
                    if (age == ConfigAge.IsOld)
                        kvreturn = (labelA) ? _fixture.KVA_Value_New : (noLabel) ? _fixture.KVA_Value_Old : null;
                    else
                        kvreturn = (labelA) ? _fixture.KVB_Value : (noLabel) ? _fixture.KVA_Value_Old : null;
                    break;
                case "superKeyVaultSetting":
                    kvreturn = (!noLabel) ? null : (age == ConfigAge.IsOld) ? _fixture.KVB_Value : _fixture.KVA_Value_Old;
                    break;
            }

            // If KeyVault is not enabled, we'll just see a URL for config values.
            if (kvreturn != null && !builder.UseAzureKeyVault)
                return kvUriRegex;
            return kvreturn;
        }

        private void ValidateExpectedConfig(AzureAppConfigurationBuilder builder, ConfigAge age)
        {
            // Run the settings through the builder
            var appSettings = GetCustomAppSettings(builder.Mode != KeyValueMode.Greedy);
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);

            // Validation of values kind of assumes this is true. Make sure of it.
            Assert.Equal(AppConfigFixture.CustomEndPoint, builder.Endpoint);

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
                if (GetExpectedConfigValue(builder, "epochDTO", age) != null)
                    expectedCount++;

                // In Greedy mode, we'll get a value for 'caseTestSetting' and then back in .Net config world, we
                // will put that in place of the already existing 'casetestsetting' value.
                var expectedValue = GetExpectedConfigValue(builder, "caseTestSetting", age) ?? "untouched";
                Assert.Equal(expectedValue, appSettings.Settings["caseTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "testSetting", age);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["testSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "newTestSetting", age);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["newTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "superTestSetting", age);
                if (expectedValue != null)
                    expectedCount++;
                Assert.Equal(expectedValue, appSettings.Settings["superTestSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "keyVaultSetting", age);
                if (expectedValue != null)
                {
                    expectedCount++;
                    Assert.Matches(expectedValue, appSettings.Settings["keyVaultSetting"]?.Value);
                }
                else
                    Assert.Null(appSettings.Settings["keyVaultSetting"]?.Value);

                expectedValue = GetExpectedConfigValue(builder, "superKeyVaultSetting", age);
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
                Assert.Equal(GetExpectedConfigValue(builder, "testSetting", age) ?? "untouched", appSettings.Settings["testSetting"]?.Value);
                Assert.Equal(GetExpectedConfigValue(builder, "newTestSetting", age) ?? "untouched", appSettings.Settings["newTestSetting"]?.Value);
                Assert.Equal(GetExpectedConfigValue(builder, "superTestSetting", age) ?? "untouched", appSettings.Settings["superTestSetting"]?.Value);
                Assert.Matches(GetExpectedConfigValue(builder, "keyVaultSetting", age) ?? "untouched", appSettings.Settings["keyVaultSetting"]?.Value);
                Assert.Matches(GetExpectedConfigValue(builder, "superKeyVaultSetting", age) ?? "untouched", appSettings.Settings["superKeyVaultSetting"]?.Value);

                Assert.Equal(6, appSettings.Settings.Count); // No 'epochDTO' in our staged config.
            }
        }

        private class BadCredentialAppConfigBuilder : AzureAppConfigurationBuilder
        {
            protected override TokenCredential GetCredential() => new ClientSecretCredential("not-a-valid-tenantid", "not-a-valid-clientid", "not-a-valid-clientsecret");

            protected override ConfigurationClientOptions GetConfigurationClientOptions() => base.GetConfigurationClientOptions();

            protected override SecretClientOptions GetSecretClientOptions() => base.GetSecretClientOptions();
        }

        private class PreloadCheckAppConfigBuilder : AzureAppConfigurationBuilder
        {
            public bool CalledGetAllValues { get; private set; } = false;

            public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
            {
                CalledGetAllValues = true;
                return base.GetAllValues(prefix);
            }
        }

        // Minimal override to force a 429 response
        private class ThrottleTestAppConfigBuilder : AzureAppConfigurationBuilder
        {
            protected override Azure.Data.AppConfiguration.ConfigurationClientOptions GetConfigurationClientOptions()
            {
                var httpClient = new HttpClient(new ThrottlingHandler());
                var options = base.GetConfigurationClientOptions();
                options.Transport = new HttpClientTransport(httpClient);
                return options;
            }

            private class ThrottlingHandler : HttpMessageHandler
            {
                protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = new HttpResponseMessage((HttpStatusCode)429)
                    {
                        Content = new StringContent("Too many requests")
                    };
                    return Task.FromResult(response);
                }
            }
        }
    }
}
