using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Configuration.ConfigurationBuilders;
using Xunit;

namespace Test
{
    public static class AzureConstants
    {
        /* Convenience to keep full-stack out of the way during local development. Leave 'false' when committing.  */
        public static readonly bool DisableFullStackTests = false;
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KeyVaultFactAttribute : FactAttribute
    {
        public KeyVaultFactAttribute(string Reason = null)
        {
            if (!AzureFixture.FullStackTestsEnabled)
                Skip = Reason ?? "Skipped: Azure KeyVault Tests Disabled.";
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class KeyVaultTheoryAttribute : TheoryAttribute
    {
        public KeyVaultTheoryAttribute(string Reason = null)
        {
            if (!AzureFixture.FullStackTestsEnabled)
                Skip = Reason ?? "Skipped: Azure KeyVault Tests Disabled.";
        }
    }

    public class AzureFixture : IDisposable
    {
        public static readonly bool FullStackTestsEnabled;
        public static readonly string CommonKeyVault;
        public static readonly string CustomKeyVault;

        public string CustomVersionCurrent { get; private set; }
        public string CustomVersionOld { get; private set; }
        public readonly string CustomVersionNotExist = "abcVersionDoesNotExistXyz";

        static AzureFixture()
        {
            CommonKeyVault = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Common");
            CustomKeyVault = Environment.GetEnvironmentVariable("Microsoft.Configuration.ConfigurationBuilders.Test.AKV.Custom");
            FullStackTestsEnabled = !AzureConstants.DisableFullStackTests && !String.IsNullOrWhiteSpace(CommonKeyVault) && !String.IsNullOrWhiteSpace(CustomKeyVault);
        }

        public AzureFixture()
        {
            // If full-stack tests are disabled, there is no need to do anything in here.
            if (!FullStackTestsEnabled) { return; }

            // The Common KeyVault gets verified/filled out, but is assumed to already exist.
            SecretClient commonClient = new SecretClient(new Uri($"https://{CommonKeyVault}.vault.azure.net"), new DefaultAzureCredential());
            foreach (string key in CommonBuilderTests.CommonKeyValuePairs)
            {
                EnsureCurrentSecret(commonClient, key, CommonBuilderTests.CommonKeyValuePairs[key]);
            }

            // The Custom KeyVault also gets verified/filled out and also is assumed to already exist.
            //      mapped-test-key == mappedValue
            //      versioned-key == versionedValue-Current
            //                       versionedValue-Older
            // The actual version string of the versioned key does not matter. Just have at least two enabled versions.
            SecretClient customClient = new SecretClient(new Uri($"https://{CustomKeyVault}.vault.azure.net"), new DefaultAzureCredential());
            EnsureCurrentSecret(customClient, "mapped-test-key", "mappedValue");
            // Secrets that get created in the absence of the correct key/value are the active version by default. Be sure to
            // check for the expected active value last.
            var vSecret = EnsureActiveSecret(customClient, "versioned-key", "versionedValue-Older");
            CustomVersionOld = vSecret.Properties.Version;
            vSecret = EnsureCurrentSecret(customClient, "versioned-key", "versionedValue-Current");
            CustomVersionCurrent = vSecret.Properties.Version;
        }

        internal static KeyVaultSecret EnsureActiveSecret(SecretClient client, string key, string value)
        {
            try
            {
                foreach (var prop in client.GetPropertiesOfSecretVersions(key))
                {
                    var secret = client.GetSecret(key, prop.Version).Value;

                    if (secret.Value == value)
                    {
                        // Make sure it's active
                        if (!secret.Properties.Enabled.GetValueOrDefault())
                        {
                            secret.Properties.Enabled = true;
                            client.UpdateSecretProperties(secret.Properties);
                        }

                        return secret;
                    }
                }
            }
            catch (RequestFailedException) { }

            // Didn't find the secret, so create it now.
            return client.SetSecret(key, value);
        }

        internal static KeyVaultSecret EnsureCurrentSecret(SecretClient client, string key, string value)
        {
            try
            {
                var secret = client.GetSecret(key).Value;

                if (secret != null && secret.Value == value)
                {
                    // Make sure it's active
                    if (!secret.Properties.Enabled.GetValueOrDefault())
                    {
                        secret.Properties.Enabled = true;
                        client.UpdateSecretProperties(secret.Properties);
                    }

                    return secret;
                }
            }
            catch (RequestFailedException) { }

            // Didn't find the secret, so create it now.
            return client.SetSecret(key, value);
        }

        public void Dispose()
        {
            // We can leave the KeyVault as is. It will get re-used by future runs if it's still there.
        }
    }


    public class AzureTests : IClassFixture<AzureFixture>
    {
        private static readonly string placeholderKeyVault = "placeholder-KeyVault";
        private readonly AzureFixture _fixture;

        public AzureTests(AzureFixture fixture)
        {
            _fixture = fixture;
        }

        // ======================================================================
        //   CommonBuilderTests
        // ======================================================================
        [KeyVaultTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void AzureKeyVault_GetValue(bool preload)
        {
            var s_preload = preload ? "Preload" : "";
            CommonBuilderTests.GetValue(() => new AzureKeyVaultConfigBuilder(), $"AzureKeyVault{s_preload}GetValue",
                new NameValueCollection() { { "vaultName", AzureFixture.CommonKeyVault }, { "preloadSecretNames", preload.ToString() } });
        }

        [KeyVaultFact]
        public void AzureKeyVault_GetAllValues()
        {
            // Preload must be enabled for GetAllValues to work, which should be the default.
            CommonBuilderTests.GetAllValues(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultGetAllValues",
                new NameValueCollection() { { "vaultName", AzureFixture.CommonKeyVault } }, GetValueFromVersionedCollection);
        }

        [KeyVaultFact]
        public void AzureKeyVault_ProcessConfigurationSection()
        {
            // The _fixture.Common test will try Greedy and Strict modes, so it only makes sense to test with preload enabled.
            CommonBuilderTests.ProcessConfigurationSection(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultProcessConfig",
                new NameValueCollection() { { "vaultName", AzureFixture.CommonKeyVault } });
        }


        // ======================================================================
        //   AzureKeyVault parameters
        // ======================================================================
        [Fact]
        public void AzureKeyVault_DefaultSettings()
        {
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultDefault",
                new NameValueCollection() { { "vaultName", placeholderKeyVault } });

            // VaultName, Uri
            Assert.Equal(placeholderKeyVault, builder.VaultName);
            Assert.Equal($"https://{placeholderKeyVault}.vault.azure.net", builder.Uri);

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

        [Fact]
        public void AzureKeyVault_Settings()
        {
            // VaultName is case insensitive, Uri follows.
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings1",
                new NameValueCollection() { { "VauLTnaMe", placeholderKeyVault } });
            Assert.Equal(placeholderKeyVault, builder.VaultName);
            Assert.Equal($"https://{placeholderKeyVault}.vault.azure.net", builder.Uri);

            // Empty Uri, but valid vaultName
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings2",
                new NameValueCollection() { { "vaultName", placeholderKeyVault }, { "uri", "" } });
            Assert.Equal($"https://{placeholderKeyVault}.vault.azure.net", builder.Uri);
            Assert.Equal(placeholderKeyVault, builder.VaultName);

            // Uri is case insensitive, VaultName is not inferred.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings3",
                new NameValueCollection() { { "uRI", $"https://{placeholderKeyVault}.VaulT.Azure.NET" } });
            Assert.Equal($"https://{placeholderKeyVault}.VaulT.Azure.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Both Uri and VaultName. Uri wins.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings4",
                new NameValueCollection() { { "vaultName", placeholderKeyVault }, { "uri", $"https://{placeholderKeyVault}.vAUlt.AzUre.NET" } });
            Assert.Equal($"https://{placeholderKeyVault}.vAUlt.AzUre.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Uri with invalid VaultName. Uri wins and everything is ok.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings5",
                new NameValueCollection() { { "vaultName", "invalid_vault_name" }, { "uri", $"https://{placeholderKeyVault}.vAUlt.AzUre.NET" } });
            Assert.Equal($"https://{placeholderKeyVault}.vAUlt.AzUre.NET", builder.Uri);
            Assert.Null(builder.VaultName);

            // Preload is case insensitive for name and value.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings6",
                new NameValueCollection() { { "vaultName", placeholderKeyVault }, { "PreLoadsecretNAMEs", "TrUe" } });
            Assert.Equal(placeholderKeyVault, builder.VaultName);
            Assert.Equal($"https://{placeholderKeyVault}.vault.azure.net", builder.Uri);
            Assert.True(builder.Preload);

            // These tests require executing the builder, which needs a valid endpoint.
            if (AzureFixture.FullStackTestsEnabled)
            {
                // Request secrets with mapped characters. Two keys => same secret is ok. Strict [CharMapping happens before GetValue; use PCS()]
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings7",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault } });
                ValidateExpectedConfig(builder);

                // Request secrets with mapped characters. Two keys => same secret is ok. Greedy [CharMapping happens before GetAllValues; use PCS()]
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultSettings8",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "mode", "Greedy" } });
                ValidateExpectedConfig(builder);
            }
        }

        [KeyVaultTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void AzureKeyVault_Version(bool preload)
        {
            // Version is case insensitive
            var builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion1",
                new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "vERsIOn", _fixture.CustomVersionCurrent }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(AzureFixture.CustomKeyVault, builder.VaultName);
            Assert.Equal($"https://{AzureFixture.CustomKeyVault}.vault.azure.net", builder.Uri);
            Assert.Equal(_fixture.CustomVersionCurrent, builder.Version);

            // No version only matches unversioned key, with the latest version of the secret.
            // Versioned keys are untouched.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion2",
                new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Null(builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion3",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Null(builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Old Version only matches versioned keys with the same version.
            // Unversioned keys are untouched... but might be replaced by the version-stripped key that matched.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion4",
                new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionOld }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(_fixture.CustomVersionOld, builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion5",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionOld }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(_fixture.CustomVersionOld, builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Current Version only matches versioned keys with the same version - not unversioned keys, even though they could be considered "current."
            // However... the unversioned key will probably be replaced by the version-stripped key that did match.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion6",
                new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionCurrent }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(_fixture.CustomVersionCurrent, builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion7",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionCurrent }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(_fixture.CustomVersionCurrent, builder.Version);
                ValidateExpectedConfig(builder);
            }

            // Version that doesn't exist does nothing.
            builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion8",
                new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionNotExist + "reallyDoesNotExist" }, { "preloadSecretNames", preload.ToString() } });
            Assert.Equal(KeyValueMode.Strict, builder.Mode);
            Assert.Equal(_fixture.CustomVersionNotExist + "reallyDoesNotExist", builder.Version);
            ValidateExpectedConfig(builder);

            if (preload) // Greedy only works with preload turned on
            {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultVersion9",
                    new NameValueCollection() { { "vaultName", AzureFixture.CustomKeyVault }, { "version", _fixture.CustomVersionNotExist + "reallyDoesNotExist" }, { "mode", "Greedy" } });
                Assert.Equal(KeyValueMode.Greedy, builder.Mode);
                Assert.Equal(_fixture.CustomVersionNotExist + "reallyDoesNotExist", builder.Version);
                ValidateExpectedConfig(builder);
            }
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
                TestHelper.ValidateWrappedException<RequestFailedException>(exception, builder);
            else
                Assert.Null(exception);

            // ConnectionString and vaultName given
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors5",
                    new NameValueCollection() { { "vaultName", placeholderKeyVault }, { "ConNecTioNstRinG", "does not matter" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors5");
            else
                Assert.Null(exception);

            // ConnectionString and Uri given
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors6",
                    new NameValueCollection() { { "uri", $"https://{placeholderKeyVault}.vault.azure.net" }, { "ConNecTioNstRinG", "does not matter" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors6");
            else
                Assert.Null(exception);

            // !Preload and Greedy disagree
            exception = Record.Exception(() => {
                builder = TestHelper.CreateBuilder<AzureKeyVaultConfigBuilder>(() => new AzureKeyVaultConfigBuilder(), "AzureKeyVaultErrors7",
                    new NameValueCollection() { { "vaultName", placeholderKeyVault }, { "mode", "Greedy" }, { "preloadSecretNames", "false" }, { "enabled", enabled.ToString() } });
            });
            if (enabled != KeyValueEnabled.Disabled)
                TestHelper.ValidateWrappedException<ArgumentException>(exception, "AzureKeyVaultErrors7");
            else
                Assert.Null(exception);

            // These tests require a valid KeyVault endpoint to get past client creation and into deeper errors.
            if (AzureFixture.FullStackTestsEnabled)
            {
                // Unauthorized access
                exception = Record.Exception(() =>
                {
                    builder = TestHelper.CreateBuilder<BadCredentialKeyVaultConfigBuilder>(() => new BadCredentialKeyVaultConfigBuilder(), "AzureKeyVaultErrors8",
                        new NameValueCollection() { { "vaultName", AzureFixture.CommonKeyVault }, { "enabled", enabled.ToString() } });
                    // Azure SDK connects lazily, so the invalid credential won't be caught until we make the KV client try to use it.
                    builder.ProcessConfigurationSection(TestHelper.GetAppSettings());
                });
                if (enabled == KeyValueEnabled.Enabled)
                    TestHelper.ValidateWrappedException<AuthenticationFailedException>(exception, builder);
                else
                    Assert.Null(exception);
            }
        }


        // ======================================================================
        //   Helpers
        // ======================================================================
        // TODO: Mock SecretClient. Much work, and we'd need to inject it into the builder.
        private string GetValueFromVersionedCollection(ICollection<KeyValuePair<string, string>> collection, string key)
        {
            foreach (var kvp in collection)
            {
                var strippedKey = kvp.Key.Split(new char[] { '/' }, 2)[0];
                if (strippedKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }
            return null;
        }

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
                appSettings.Settings.Add("versioned-key/" + _fixture.CustomVersionCurrent, "untouched");
                appSettings.Settings.Add("versioned-key/" + _fixture.CustomVersionOld, "untouched");
                appSettings.Settings.Add("versioned-key/" + _fixture.CustomVersionNotExist, "untouched");
            }

            // Run the settings through the builder
            appSettings = (AppSettingsSection)builder.ProcessConfigurationSection(appSettings);

            // Validation of values kind of assumes this is true. Make sure of it.
            Assert.Equal(AzureFixture.CustomKeyVault, builder.VaultName);

            // On to the validation of expectations!
            if (builder.Mode == KeyValueMode.Greedy)
            {
                if (builder.Version == _fixture.CustomVersionCurrent)
                {
                    Assert.Single(appSettings.Settings);
                    Assert.Equal("versionedValue-Current", appSettings.Settings["versioned-key"]?.Value);
                }
                else if (builder.Version == _fixture.CustomVersionOld)
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
                if (builder.Version == _fixture.CustomVersionCurrent)
                {
                    Assert.Equal(6, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("versionedValue-Current", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + _fixture.CustomVersionCurrent]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionNotExist]?.Value);
                }
                else if (builder.Version == _fixture.CustomVersionOld)
                {
                    Assert.Equal(6, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("versionedValue-Older", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionCurrent]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + _fixture.CustomVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionNotExist]?.Value);
                }
                else if (builder.Version != null)
                {
                    Assert.Equal(7, appSettings.Settings.Count);

                    Assert.Equal("not mapped", appSettings.Settings["mapped_test_key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings[@"mapped\test+key"]?.Value);
                    Assert.Equal("not mapped", appSettings.Settings["mapped:test.key"]?.Value);

                    Assert.Equal("untouched", appSettings.Settings["versioned-key"]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionCurrent]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionNotExist]?.Value);
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
                    Assert.Null(appSettings.Settings["versioned-key/" + _fixture.CustomVersionCurrent]?.Value);
                    Assert.Null(appSettings.Settings["versioned-key/" + _fixture.CustomVersionOld]?.Value);
                    Assert.Equal("untouched", appSettings.Settings["versioned-key/" + _fixture.CustomVersionNotExist]?.Value);
                }
            }
        }

        private class BadCredentialKeyVaultConfigBuilder : AzureKeyVaultConfigBuilder
        {
            protected override TokenCredential GetCredential() => new ClientSecretCredential("not", "valid", "credential");

            protected override SecretClientOptions GetSecretClientOptions() => base.GetSecretClientOptions();
        }
    }
}
