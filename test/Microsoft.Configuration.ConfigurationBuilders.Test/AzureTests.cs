using System;
using System.Collections.Specialized;
using System.Configuration;

using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KeyVaultFactAttribute : FactAttribute
    {
        public KeyVaultFactAttribute(string Reason = null)
        {
            if (!AzureTests.AzureTestsEnabled)
                Skip = Reason ?? "Skipped: Azure KeyVault Tests Disabled.";
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KeyVaultTheoryAttribute : TheoryAttribute
    {
        public KeyVaultTheoryAttribute(string Reason = null)
        {
            if (!AzureTests.AzureTestsEnabled)
                Skip = Reason ?? "Skipped: Azure KeyVault Tests Disabled.";
        }
    }

    public class AzureTests
    {
        private readonly string commonKeyVault;
        private readonly string customKeyVault;
        private readonly string customVersionCurrent;
        private readonly string customVersionOld;
        private readonly string customVersionNotExist = "abcVersionDoesNotExistXyz";

        public static bool AzureTestsEnabled => true;

        public AzureTests()
        {
            // The Common KeyVault gets verified/filled out, but is assumed to already exist.
            commonKeyVault = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Common");
            SecretClient commonClient = new SecretClient(new Uri($"https://{commonKeyVault}.vault.azure.net"), new DefaultAzureCredential());
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                try
                {
                    var secret = commonClient.GetSecret(key).Value;

                    // Make sure it's not hidden
                    if (!secret.Properties.Enabled.GetValueOrDefault())
                    {
                        secret.Properties.Enabled = true;
                        commonClient.UpdateSecretProperties(secret.Properties);
                    }

                    // Make sure it's got the correct value
                    if (secret.Value != CommonBuilderTests.CommonKeyValuePairs[key])
                        commonClient.SetSecret(key, CommonBuilderTests.CommonKeyValuePairs[key]);
                }
                catch (RequestFailedException)
                {
                    // Didn't find the secret, so create it now.
                    commonClient.SetSecret(key, CommonBuilderTests.CommonKeyValuePairs[key]);
                }
            }

            // The Custom KeyVault is assumed to exist and already be filled out. Do just one quick sanity value check. It looks like this:
            //      mapped-test-key == mappedValue
            //      versioned-key == versionedValue-Current
            //                       versionedValue-Older
            // The actual version string of the versioned key does not matter. Just have at least two enabled versions.
            customKeyVault = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Custom");
            SecretClient customClient = new SecretClient(new Uri($"https://{customKeyVault}.vault.azure.net"), new DefaultAzureCredential());
            var vaultCheck = customClient.GetSecret("versioned-key")?.Value;
            customVersionCurrent = vaultCheck.Properties.Version;
            foreach (SecretProperties secretProps in customClient.GetPropertiesOfSecretVersions("versioned-key"))
                if (secretProps.Enabled.Value && secretProps.Version != customVersionCurrent)
                    customVersionOld = secretProps.Version;
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [KeyVaultFact]
        public void AzureKeyVault_GetValue()
        {
            CommonBuilderTests.GetValue(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultGetValue",
                new NameValueCollection() { { "vaultName", commonKeyVault }, { "preloadSecretNames", "false" } });

            CommonBuilderTests.GetValue(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultPreloadGetValue",
                new NameValueCollection() { { "vaultName", commonKeyVault }, { "preloadSecretNames", "true" } });
        }

        [KeyVaultFact]
        public void AzureKeyVault_GetAllValues()
        {
            CommonBuilderTests.GetValue(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultGetAllValues",
                new NameValueCollection() { { "vaultName", commonKeyVault }, { "preloadSecretNames", "false" } });

            CommonBuilderTests.GetValue(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultPreloadGetAllValues",
                new NameValueCollection() { { "vaultName", commonKeyVault }, { "preloadSecretNames", "true" } });
        }


        // ======================================================================
        //   AzureKeyVault parameters
        // ======================================================================
        [KeyVaultFact]
        public void AzureKeyVault_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultDefault",
                new NameValueCollection() { { "vaultName", customKeyVault } });

            // VaultName, Uri
            Assert.Equal(customKeyVault, builder.VaultName);
            Assert.Equal($"https://{customKeyVault}.vault.azure.net", builder.Uri);

            // Version
            Assert.Null(builder.Version);

            // PreloadSecretNames
            Assert.True(builder.Preload);

            // Enabled
            Assert.Equal(KeyValueEnabled.Enabled, builder.Enabled);

            // CharMap
            Assert.Equal(5, builder.CharacterMap.Count);
            Assert.Equal("-", builder.CharacterMap[":"]);
            Assert.Equal("-", builder.CharacterMap["_"]);
            Assert.Equal("-", builder.CharacterMap["."]);
            Assert.Equal("-", builder.CharacterMap["+"]);
            Assert.Equal("-", builder.CharacterMap["\\"]);
        }

        [KeyVaultFact]
        public void AzureKeyVault_Settings()
        {
            // VaultName is case insensitive, Uri follows.
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings1",
                new NameValueCollection() { { "VauLTnaMe", customKeyVault } });
            Assert.Equal(customKeyVault, builder.VaultName);
            Assert.Equal($"https://{customKeyVault}.vault.azure.net", builder.Uri);

            // Empty Uri, but valid vaultName
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings2",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "uri", "" } });
            Assert.Equal($"https://{customKeyVault}.vault.azure.net", builder.Uri);
            Assert.Equal(customKeyVault, builder.VaultName);

            // Uri is case insensitive, VaultName is not inferred.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings3",
                new NameValueCollection() { { "uRI", $"https://{customKeyVault}.VaulT.Azure.NET" } });
            Assert.Equal($"https://{customKeyVault}.VaulT.Azure.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Both Uri and VaultName. Uri wins.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings4",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "uri", $"https://{customKeyVault}.vAUlt.AzUre.NET" } });
            Assert.Equal($"https://{customKeyVault}.vAUlt.AzUre.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Uri with invalid VaultName. Uri wins and everything is ok.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings5",
                new NameValueCollection() { { "vaultName", "invalid_vault_name" }, { "uri", $"https://{customKeyVault}.vAUlt.AzUre.NET" } });
            Assert.Equal($"https://{customKeyVault}.vAUlt.AzUre.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Preload is case insensitive for name and value.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings6",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "PreLoadsecretNAMEs", "TrUe" } });
            Assert.Equal(customKeyVault, builder.VaultName);
            Assert.Equal($"https://{customKeyVault}.vault.azure.net", builder.Uri);
            Assert.True(builder.Preload);

            // Request secrets with mapped characters. Two keys => same secret is ok. Strict [CharMapping happens before GetValue; use PCS()]
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings7",
                new NameValueCollection() { { "vaultName", customKeyVault } });
            ValidateExpectedConfig(builder);

            // Request secrets with mapped characters. Two keys => same secret is ok. Greedy [CharMapping happens before GetAllValues; use PCS()]
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings8",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "mode", "Greedy" } });
            ValidateExpectedConfig(builder);
        }

        [KeyVaultTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void AzureKeyVault_Version(bool preload)
        {
            // Version is case insensitive
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion1",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "vERsIOn", customVersionCurrent }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(customKeyVault, builder.VaultName);
            Assert.Equal($"https://{customKeyVault}.vault.azure.net", builder.Uri);
            Assert.Equal(customVersionCurrent, builder.Version);

            // No version only matches unversioned key, with the latest version of the secret.
            // Versioned keys are untouched.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion2",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Null(builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion3",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Null(builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Old Version only matches versioned keys with the same version.
            // Unversioned keys are untouched... but might be replaced by the version-stripped key that matched.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion4",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionOld }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(customVersionOld, builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion5",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionOld }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(customVersionOld, builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Current Version only matches versioned keys with the same version - not unversioned keys, even though they could be considered "current."
            // However... the unversioned key will probably be replaced by the version-stripped key that did match.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion6",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionCurrent }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(customVersionCurrent, builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion7",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionCurrent }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(customVersionCurrent, builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Version that doesn't exist does nothing.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion8",
                new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionNotExist + "reallyDoesNotExist" }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(customVersionNotExist + "reallyDoesNotExist", builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion9",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "version", customVersionNotExist + "reallyDoesNotExist" }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(customVersionNotExist + "reallyDoesNotExist", builder.Version);
                ValidateExpectedConfig(builder);
            }
        }


        // ======================================================================
        //   Errors
        // ======================================================================
        [KeyVaultTheory]
        [InlineData(KeyValueEnabled.Optional)]
        [InlineData(KeyValueEnabled.Enabled)]
        [InlineData(KeyValueEnabled.Disabled)]
        public void AzureKeyVault_ErrorsOptional(KeyValueEnabled enabled)
        {
            AzureKeyVaultConfigBuilder builder = null;

            // No vault name or Uri
            var exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors1",
                    new NameValueCollection() { { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors1");
            else
                Assert.Null(exception);

            // Empty Uri, no vault name
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors2",
                    new NameValueCollection() { { "uri", "" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors2");
            else
                Assert.Null(exception);

            // Empty vault name and Uri
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors3",
                    new NameValueCollection() { { "uri", "" }, { "vaultName", "" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors3");
            else
                Assert.Null(exception);

            // Invalid vault name (contains '_' for example)
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors4",
                    new NameValueCollection() { { "vaultName", "name_with_underscores" }, { "enabled", enabled.ToString() } });
                // We connect lazily, so the invalid name won't be caught until we try to use it.
                builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
            });
            if (enabled == KeyValueEnabled.Enabled)
                TestHelper.ValidateWrappedException(exception, builder);
            else
                Assert.Null(exception);

            // ConnectionString and vaultName given
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors5",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "ConNecTioNstRinG", "does not matter" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors5");
            else
                Assert.Null(exception);

            // ConnectionString and Uri given
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors6",
                    new NameValueCollection() { { "uri", $"https://{customKeyVault}.vault.azure.net" }, { "ConNecTioNstRinG", "does not matter" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors6");
            else
                Assert.Null(exception);

            // !Preload and Greedy disagree
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors7",
                    new NameValueCollection() { { "vaultName", customKeyVault }, { "mode", "Greedy" }, { "preloadSecretNames", "false" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors7");
            else
                Assert.Null(exception);

            // TODO - How to test for unauthorized access to a vault? Access Denied is an optional error.
        }


        // ======================================================================
        //   Helpers
        // ======================================================================
        // TODO: Mock SecretClient. Much work, and we'd need to inject it into the builder.
        private void ValidateExpectedConfig(AzureKeyVaultConfigBuilder builder)
        {
            var cfg = TestHelper.LoadMultiLevelConfig("empty.config", "customAppSettings.config");
            var appSettings = cfg.AppSettings;

            // Prime the appSettings section as expected for this test
            if (builder.Mode != KeyValueMode.Greedy)
            {
                appSettings.Settings.Add("mapped_test_key", "not mapped");
                appSettings.Settings.Add(@"mapped\test+key", "not mapped");
                appSettings.Settings.Add("mapped:test.key", "not mapped");

                appSettings.Settings.Add("versioned-key", "untouched");
                appSettings.Settings.Add("versioned-key/" + customVersionCurrent, "untouched");
                appSettings.Settings.Add("versioned-key/" + customVersionOld, "untouched");
                appSettings.Settings.Add("versioned-key/" + customVersionNotExist, "untouched");
            }

            // Run the settings through the builder
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);

            // Validation of values kind of assumes this is true. Make sure of it.
            Assert.Equal(customKeyVault, builder.VaultName);

            // On to the validation of expectations!
            if (builder.Mode == KeyValueMode.Greedy)
            {
                if (builder.Version == customVersionCurrent)
                {
                    Assert.Single(appSettings.Settings);
                    Assert.Equal("versionedValue-Current", appSettings.Settings["versioned-key"]?.Value);
                }
                else if (builder.Version == customVersionOld)
                {
                    Assert.Single(appSettings.Settings);
                    Assert.Equal("versionedValue-Older", appSettings.Settings["versioned-key"]?.Value);
                }
                else if (builder.Version != null)
                {
                    Assert.Empty(appSettings.Settings);
                }
                else
                {
                    Assert.Equal(2, appSettings.Settings.Count);
                    Assert.Equal("mappedValue", appSettings.Settings["mapped-test-key"]?.Value);
                    Assert.Equal("versionedValue-Current", appSettings.Settings["versioned-key"]?.Value);
                }
            }
            else
            {
                if (builder.Version == customVersionCurrent)
                {
                    Assert.Equal(6, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("versionedValue-Current", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + customVersionCurrent]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionNotExist]?.Value);
                }
                else if (builder.Version == customVersionOld)
                {
                    Assert.Equal(6, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("versionedValue-Older", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionCurrent]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + customVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionNotExist]?.Value);
                }
                else if (builder.Version != null)
                {
                    Assert.Equal(7, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("untouched", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionCurrent]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionNotExist]?.Value);
                }
                else
                {
                    Assert.Equal(5, appSettings.Settings.Count);

                    Assert.Equal("mappedValue", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("mappedValue", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("mappedValue", appSettings.Settings["mapped:test.key"]?.Value);

                    // Versions in the key name will work in the absence of a version on the builder.
                    // But they all strip-down to the same key name.
                    // Which one wins is just a matter of order. 'Older' was purposely the last of
                    // the three added to the settings here, so it should be the "last winner."
                    // The non-existent version is not found though, so it is left unchanged.
                    Assert.Equal("versionedValue-Older", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + customVersionCurrent]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + customVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + customVersionNotExist]?.Value);
                }
            }
        }
    }
}
