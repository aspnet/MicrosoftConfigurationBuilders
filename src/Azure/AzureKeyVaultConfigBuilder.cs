// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Azure;
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
        public const string connectionStringTag = "connectionString";
        public const string uriTag = "uri";
        public const string versionTag = "version";
        public const string preloadTag = "preloadSecretNames";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private string _vaultName;
        private string _connectionString;
        private string _uri;
        private string _version;
        private bool _preload;
        private bool _preloadFailed;

        private SecretClient _kvClient;
        private List<string> _allKeys;

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Default 'Optional' to false. base.LazyInitialize() will override if specified in config.
            Optional = false;

            base.LazyInitialize(name, config);

            if (!Boolean.TryParse(UpdateConfigSettingWithAppSettings(preloadTag), out _preload))
                _preload = true;
            if (!_preload && Mode == KeyValueMode.Greedy)
                throw new ArgumentException($"'{preloadTag}'='false' is not compatible with {KeyValueMode.Greedy} mode.");

            _uri = UpdateConfigSettingWithAppSettings(uriTag);
            _vaultName = UpdateConfigSettingWithAppSettings(vaultNameTag);
            _version = UpdateConfigSettingWithAppSettings(versionTag);
            if (String.IsNullOrWhiteSpace(_version))
                _version = null;

            if (String.IsNullOrWhiteSpace(_uri))
            {
                if (String.IsNullOrWhiteSpace(_vaultName))
                {
                    if (Optional)
                    {
                        return;
                    }

                    throw new ArgumentException($"Vault must be specified by name or URI using the '{vaultNameTag}' or '{uriTag}' attribute.");
                }
                else
                {
                    _uri = $"https://{_vaultName}.vault.azure.net";
                }
            }
            _uri = _uri.TrimEnd(new char[] { '/' });

            _connectionString = UpdateConfigSettingWithAppSettings(connectionStringTag);
            _connectionString = String.IsNullOrWhiteSpace(_connectionString) ? null : _connectionString;

            // Connect to KeyVault
            try
            {
                _kvClient = new SecretClient(new Uri(_uri), new DefaultAzureCredential());
            }
            catch (Exception)
            {
                if (!Optional)
                    throw;
                _kvClient = null;
            }

            if (_preload)
            {
                _allKeys = GetAllKeys();
            }
        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' for the secret to look up in the configured Key Vault. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            // Azure Key Vault keys are case-insensitive, so this should be fine.
            // Also, this is a synchronous method. And in single-threaded contexts like ASP.Net
            // it can be bad/dangerous to block on async calls. So lets work some TPL voodoo
            // to avoid potential deadlocks.
            return Task.Run(async () => { return await GetValueAsync(key); }).Result?.Value;
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

            foreach (string key in _allKeys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    tasks.Add(Task.Run(() => GetValueAsync(key).ContinueWith(t =>
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
        /// Transform the given key to an intermediate format that will be used to look up values in backing store.
        /// </summary>
        /// <param name="key">The string to be mapped.</param>
        /// <returns>The key string to be used while looking up config values..</returns>
        public override string MapKey(string key)
        {
            if (String.IsNullOrEmpty(key))
                return key;

            // Colons and underscores are common in appSettings keys, but not allowed in key vault key names.
            // It's likely that apps will want to lookup config values with these characters in their name in
            // key vault. More extensive key mapping can be done with subclasses. But let's handle the most
            // most common case here.
            key = key.Replace(':', '-');
            key = key.Replace('_', '-');
            return key;
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

        private async Task<KeyVaultSecret> GetValueAsync(string key)
        {
            if (_kvClient == null)
                return null;

            VersionedKey vKey = new VersionedKey(key);

            // If we successfully preloaded key names, see if the requested key is valid before making network request.
            if (!_preload || _preloadFailed || _allKeys.Contains(vKey.Key, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string version = _version ?? vKey.Version;
                    if (version != null)
                    {
                        KeyVaultSecret versionedSecret = await _kvClient.GetSecretAsync(vKey.Key, version);
                        return versionedSecret;
                    }

                    KeyVaultSecret secret = await _kvClient.GetSecretAsync(vKey.Key);
                    return secret;
                }
                catch (RequestFailedException rfex)
                {
                    // Simply return null if the secret wasn't found
                    //if (rfex.ErrorCode == "SecretNotFound" || rfex.ErrorCode == "BadParameter")
                    // .ErrorCode doesn't get populated. :/
                    // "SecretNotFound" == 404
                    // "BadParameter" = 400
                    if (rfex.Status == 404 || rfex.Status == 400)
                        return null;

                    // If there was a permission issue or some other error, let the exception bubble
                    // FYI: kve.Body.Error.Code == "Forbidden" :: No Rights, or secret is disabled.
                    if (!Optional)
                        throw;
                }
            }

            return null;
        }

        private List<string> GetAllKeys()
        {
            List<string> keys = new List<string>(); // KeyVault keys are case-insensitive. There won't be case-duplicates. List<> should be fine.

            if (_kvClient == null)
                return keys;

            try
            {
                foreach (SecretProperties secretProps in _kvClient.GetPropertiesOfSecrets())
                {
                    // Don't include disabled secrets
                    if (!secretProps.Enabled.GetValueOrDefault())
                        continue;

                    keys.Add(secretProps.Name);
                }
            }
            catch (RequestFailedException rfex)
            {
                _preloadFailed = true;

                // If List Permission on Secrets in not available return empty list of keys
                if (rfex.ErrorCode == "Forbidden")
                    return keys;

                if (!Optional)
                    throw;
            }

            return keys;
        }

        class VersionedKey
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
