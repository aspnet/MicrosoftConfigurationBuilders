﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that retrieves values from Azure Key Vault.
    /// </summary>
    public class AzureKeyVaultConfigBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string vaultNameTag = "vaultName";
        public const string connectionStringTag = "connectionString";   // obsolete
        public const string uriTag = "uri";
        public const string versionTag = "version";
        public const string preloadTag = "preloadSecretNames";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        /// <summary>
        /// Gets or sets the name of the Azure Key Vault to connect to. Used in the construction of default Uri.
        /// </summary>
        public string VaultName { get; protected set; }

        /// <summary>
        /// Gets or sets the specific Uri used to connect to Azure Key Vault. (May be inferred based on <see cref="VaultName"/>.)
        /// </summary>
        public string Uri { get; protected set; }

        /// <summary>
        /// Gets or sets a version string used to retrieve specific versions of secrets from the vault.
        /// </summary>
        public string Version { get; protected set; }

        /// <summary>
        /// Gets or sets a property indicating whether the builder should request a list of all keys from the vault before
        /// looking up secrets. (This knowledge may reduce the number of requests made to KeyVault, but could also bring
        /// large amounts of data into memory that may be unwanted.)
        /// </summary>
        public bool Preload { get; protected set; }

        private SecretClient _kvClient;
        private Lazy<List<string>> _allKeys;

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Default to 'Enabled'. base.LazyInitialize() will override if specified in config.
            Enabled = KeyValueEnabled.Enabled;

            // Key Vault names can only contain [a-zA-Z0-9] and '-'.
            // https://docs.microsoft.com/en-us/azure/key-vault/general/about-keys-secrets-certificates
            // That's a lot of disallowed characters to map away. Fortunately, 'charMap' allows users
            // to do this on a per-case basis. But let's cover some common cases by default.
            // Don't add '/' to the map though, as that will mess up versioned keys.
            CharacterMap.Add(":", "-");
            CharacterMap.Add("_", "-");
            CharacterMap.Add(".", "-");
            CharacterMap.Add("+", "-");
            CharacterMap.Add(@"\", "-");

            base.LazyInitialize(name, config);

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (Enabled == KeyValueEnabled.Disabled) return;

            // It's lazy, but if something goes off-track before we do this... well, we'd at least like to
            // work with an empty list rather than a null list. So do this up front.
            _allKeys = new Lazy<List<string>>(() => GetAllKeys(), System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);

            Preload = true;
            if (Boolean.TryParse(UpdateConfigSettingWithAppSettings(preloadTag), out bool _preload))
                Preload = _preload;
            if (!Preload && Mode == KeyValueMode.Greedy)
                throw new ArgumentException($"'{preloadTag}'='false' is not compatible with {KeyValueMode.Greedy} mode.");

            Uri = UpdateConfigSettingWithAppSettings(uriTag);

            if (String.IsNullOrWhiteSpace(Uri))
            {
                VaultName = UpdateConfigSettingWithAppSettings(vaultNameTag);
                if (String.IsNullOrWhiteSpace(VaultName))
                {
                    throw new ArgumentException($"Vault must be specified by name or URI using the '{vaultNameTag}' or '{uriTag}' attribute.");
                }
                else
                {
                    Uri = $"https://{VaultName}.vault.azure.net";
                }
            }
            Uri = Uri.TrimEnd(new char[] { '/' });

            Version = UpdateConfigSettingWithAppSettings(versionTag);
            if (String.IsNullOrWhiteSpace(Version))
                Version = null;

            if (config[connectionStringTag] != null)
            {
                // A connection string was given. Connection strings are no longer supported. Azure.Identity is the preferred way to
                // authenticate, and that library has various mechanisms other than a plain text connection string in config to obtain
                // the necessary client credentials for connecting to Azure.
                // Be noisy about this even if optional, as it is a fundamental misconfiguration going forward.
                throw new ArgumentException("AzureKeyVaultConfigBuilder no longer supports connection strings as of version 2.", connectionStringTag);
            }

            // Connect to KeyVault
            try
            {
                _kvClient = new SecretClient(new Uri(Uri), GetCredential(), GetSecretClientOptions());
            }
            catch (Exception)
            {
                if (!IsOptional)
                    throw;
                _kvClient = null;
            }
        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' for the secret to look up in the configured Key Vault. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            VersionedKey vKey = new VersionedKey(key);

            // Don't get versioned keys that don't match the builder version
            if (Version != null && vKey.Version != Version)
                return null;

            // Only hit the network if we didn't preload, or if we know the key exists after preloading.
            if (!Preload || _allKeys.Value.Contains(vKey.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Azure Key Vault keys are case-insensitive, so this should be fine.
                // vKey.Version here is either the same as this.Version or this.Version is null
                // Also, this is a synchronous method. And in single-threaded contexts like ASP.Net
                // it can be bad/dangerous to block on async calls. So lets work some TPL voodoo
                // to avoid potential deadlocks.
                return Task.Run(async () => { return await GetValueAsync(vKey.Key, vKey.Version); }).Result?.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets a <see cref="TokenCredential"/> to authenticate with KeyVault. This defaults to <see cref="DefaultAzureCredential"/>.
        /// </summary>
        /// <returns>A token credential.</returns>
        protected virtual TokenCredential GetCredential() => new DefaultAzureCredential();

        /// <summary>
        /// Gets a <see cref="SecretClientOptions"/> to initialize the Key Vault <see cref="SecretClient"/> with. This defaults to a new <see cref="SecretClientOptions"/>.
        /// </summary>
        /// <returns>SecretClientOptions instance.</returns>
        protected virtual SecretClientOptions GetSecretClientOptions() => new SecretClientOptions();

        /// <summary>
        /// Returns a Boolean value indicating whether the given exception is should be considered an optional issue that
        /// should be ignored or whether the exception should bubble up. This should consult <see cref="KeyValueConfigBuilder.IsOptional"/>.
        /// </summary>
        /// <param name="e"></param>
        /// <returns>A Boolean to indicate whether the exception should be ignored.</returns>
        // TODO: This should be considered for moving into KeyValueConfigBuilder as a virtual method in a major update.
        // But for now, leave it here since we don't want to force a hard tie between minor versions of these packages.
        protected bool ExceptionIsOptional(Exception e)
        {
            // Failed Azure requests have different meanings
            if (e is RequestFailedException rfex)
            {
                // .ErrorCode doesn't always get populated. :/ But we can still check HTTP status.
                // "BadParameter" = 400
                // "Forbidden" == 403
                // "SecretNotFound" == 404

                // Secret wasn't found - This is ok at all times. It just means we asked for a value
                // and Key Vault doesn't have it. Move along.
                if (rfex.Status == 404 || rfex.Status == 400)
                    return true;

                // Access was denied
                if (rfex.Status == 403)
                    return IsOptional;

                // There was an error connecting over the web. DNS, timeout, etc.
                if (rfex.InnerException is System.Net.WebException we)
                    return IsOptional ;
            }

            // All Auth exceptions are potentially optional
            if (e is AuthenticationRequiredException || e is AuthenticationFailedException || e is CredentialUnavailableException)
                return IsOptional;

            // Even when 'optional', don't catch things unless we're certain we know what it is.
            return false;
        }


        /// <summary>
        /// Retrieves all known key/value pairs from the Key Vault where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            ConcurrentDictionary<string, string> d = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<Task> tasks = new List<Task>();

            foreach (string key in _allKeys.Value)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    tasks.Add(Task.Run(() => GetValueAsync(key, Version).ContinueWith(t =>
                    {
                        // Azure Key Vault keys are case-insensitive, so there shouldn't be any races here.
                        // Include version information. It will get filtered out later before updating config.
                        KeyVaultSecret secret = t.Result;
                        if (secret != null)
                        {
                            string versionedKey = key + "/" + (secret.Properties?.Version ?? "0");
                            d[versionedKey] = secret.Value;
                        }
                    })));
            }
            Task.WhenAll(tasks).Wait();

            return d;
        }

        /// <summary>
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public override bool ValidateKey(string key)
        {
            // Key Vault only allows alphanumerics and '-'. This builder also allows for one '/' for versioning.
            return Regex.IsMatch(key, "^[a-zA-Z0-9-]+(/?[a-zA-Z0-9-]+)?$");
        }

        /// <summary>
        /// Transforms the raw key to a new string just before updating items in Strict and Greedy modes.
        /// </summary>
        /// <param name="rawKey">The key as read from the incoming config section.</param>
        /// <returns>The key string that will be left in the processed config section.</returns>
        public override string UpdateKey(string rawKey)
        {
            // Remove the version segment if it's there.
            return new VersionedKey(rawKey).Key;
        }

        private async Task<KeyVaultSecret> GetValueAsync(string key, string version)
        {
            if (_kvClient == null)
                return null;

            try
            {
                KeyVaultSecret secret = await _kvClient.GetSecretAsync(key, version);

                if (secret != null && secret.Properties.Enabled.GetValueOrDefault())
                    return secret;
            }
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return null;
        }

        private List<string> GetAllKeys()
        {
            List<string> keys = new List<string>(); // KeyVault keys are case-insensitive. There won't be case-duplicates. List<> should be fine.

            // Don't go loading all the keys if we can't, or if we were told not to
            if (_kvClient == null || !Preload)
                return keys;

            try
            {
                foreach (var secretProps in _kvClient.GetPropertiesOfSecrets())
                {
                    // Don't include disabled secrets
                    if (!secretProps.Enabled.GetValueOrDefault())
                        continue;

                    keys.Add(secretProps.Name);
                }
            }
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return keys;
        }

        private class VersionedKey
        {
            public string Key;
            public string Version;

            public VersionedKey(string fullKey)
            {
                string[] parts = fullKey.Split(new char[] { '/' }, 2);
                Key = parts[0];
                if (parts.Length > 1)
                    Version = parts[1];
            }
        }
    }
}
