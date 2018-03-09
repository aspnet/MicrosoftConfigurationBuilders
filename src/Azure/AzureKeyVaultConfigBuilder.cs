// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public class AzureKeyVaultConfigBuilder : KeyValueConfigBuilder
    {
        public const string vaultNameTag = "vaultName";
        public const string connectionStringTag = "connectionString";
        public const string uriTag = "uri";

        private string _vaultName;
        private string _connectionString;
        private string _uri;

        private KeyVaultClient _kvClient;
        private List<string> _allKeys;

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            _uri = config?[uriTag];
            _vaultName = config?[vaultNameTag];

            if (String.IsNullOrWhiteSpace(_uri))
            {
                if (String.IsNullOrWhiteSpace(_vaultName))
                    throw new ArgumentException($"AzureKeyVaultConfigBuilder {name}: Vault must be specified by name or URI using the '{vaultNameTag}' or '{uriTag}' attribute.");
                else
                    _uri = $"https://{_vaultName}.vault.azure.net";
            }
            _uri = _uri.TrimEnd(new char[] { '/' });

            _connectionString = config?[connectionStringTag];
            _connectionString = String.IsNullOrWhiteSpace(_connectionString) ? null : _connectionString;

            // Connect to KeyValut
            AzureServiceTokenProvider tokenProvider = new AzureServiceTokenProvider(_connectionString);
            _kvClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(tokenProvider.KeyVaultTokenCallback));

            _allKeys = GetAllKeys();
        }

        public override string GetValue(string key)
        {
            // Azure Key Vault keys are case-insensitive, so this should be fine.
            return GetValueAsync(key).Result;
        }

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
            if (_allKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                var secret = await _kvClient.GetSecretAsync(_uri, key);
                return secret?.Value;
            }

            return null;
        }

        private List<string> GetAllKeys()
        {
            var allSecrets = Task.Run(async () => { return await _kvClient.GetSecretsAsync(_uri); }).Result;

            List<Task> tasks = new List<Task>();
            List<string> keys = new List<string>(); // KeyVault keys are case-insensitive. There won't be case-duplicates. List<> should be fine.

            foreach (var secretItem in allSecrets)
                keys.Add(secretItem.Identifier.Name);

            return keys;
        }
    }
}
