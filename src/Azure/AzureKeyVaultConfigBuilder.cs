﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

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

        private KeyVaultClient _kvClient;
        private List<string> _allKeys;

        /// <summary>
        /// Initializes the configuration builder.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            if (!Boolean.TryParse(config?[preloadTag], out _preload))
                _preload = true;
            if (!_preload && Mode == KeyValueMode.Greedy)
                throw new ArgumentException($"'{preloadTag}'='false' is not compatible with {KeyValueMode.Greedy} mode.");

            _uri = config?[uriTag];
            _vaultName = config?[vaultNameTag];
            _version = config?[versionTag];

            if (String.IsNullOrWhiteSpace(_uri))
            {
                if (String.IsNullOrWhiteSpace(_vaultName))
                    throw new ArgumentException($"Vault must be specified by name or URI using the '{vaultNameTag}' or '{uriTag}' attribute.");
                else
                    _uri = $"https://{_vaultName}.vault.azure.net";
            }
            _uri = _uri.TrimEnd(new char[] { '/' });

            _connectionString = config?[connectionStringTag];
            _connectionString = String.IsNullOrWhiteSpace(_connectionString) ? null : _connectionString;

            // Connect to KeyValut
            AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider(_connectionString);
            _kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));

            if (_preload) {
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
            return Task.Run(async () => { return await GetValueAsync(key); }).Result;
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
                    tasks.Add(Task.Run(() => GetValueAsync(key).ContinueWith(secret =>
                    {
                        // Azure Key Vault keys are case-insensitive, so there shouldn't be any races here.
                        d[key] = secret.Result;
                    })));
            }
            Task.WhenAll(tasks).Wait();

            return d;
        }

        private async Task<string> GetValueAsync(string key)
        {
            if (!_preload || _allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!String.IsNullOrWhiteSpace(_version))
                    {
                        var versionedSecret = await _kvClient.GetSecretAsync(_uri, key, _version);
                        return versionedSecret?.Value;
                    }

                    var secret = await _kvClient.GetSecretAsync(_uri, key);
                    return secret?.Value;
                } catch (KeyVaultErrorException kve) {
                    // Simply return null if the secret wasn't found
                    if (kve.Body.Error.Code == "SecretNotFound")
                        return null;

                    // If there was a permission issue or some other error, let the exception bubble
                    // FYI: kve.Body.Error.Code == "Forbidden" :: No Rights, or secret is disabled.
                    throw;
                }
            }

            return null;
        }

        private List<string> GetAllKeys()
        {
            List<string> keys = new List<string>(); // KeyVault keys are case-insensitive. There won't be case-duplicates. List<> should be fine.

            // Get first page of secret keys
            var allSecrets = Task.Run(async () => { return await _kvClient.GetSecretsAsync(_uri); }).Result;
            foreach (var secretItem in allSecrets)
                keys.Add(secretItem.Identifier.Name);

            // If there more more pages, get those too
            string nextPage = allSecrets.NextPageLink;
            while (!String.IsNullOrWhiteSpace(nextPage))
            {
                var moreSecrets = Task.Run(async () => { return await _kvClient.GetSecretsNextAsync(nextPage); }).Result;
                foreach (var secretItem in moreSecrets)
                    keys.Add(secretItem.Identifier.Name);
                nextPage = moreSecrets.NextPageLink;
            }

            return keys;
        }
    }
}
