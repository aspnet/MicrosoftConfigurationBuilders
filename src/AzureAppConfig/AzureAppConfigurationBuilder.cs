// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.AppConfiguration.Azconfig;
using Microsoft.Azure.AppConfiguration.ManagedIdentityConnector;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that retrieves values from Azure App Configuration stores.
    /// </summary>
    public class AzureAppConfigurationBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string endpointTag = "endpoint";
        public const string connectionStringTag = "connectionString";
        public const string keyFilterTag = "keyFilter";
        public const string labelFilterTag = "labelFilter";
        public const string dateTimeFilterTag = "preferredDateTime";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private Uri _endpoint;
        private string _connectionString;
        private string _keyFilter;
        private string _labelFilter;
        private DateTimeOffset _dateTimeFilter;

        private AzconfigClient _client;

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

            _keyFilter = UpdateConfigSettingWithAppSettings(keyFilterTag);
            _labelFilter = UpdateConfigSettingWithAppSettings(labelFilterTag);
            _dateTimeFilter = DateTimeOffset.TryParse(UpdateConfigSettingWithAppSettings(dateTimeFilterTag), out _dateTimeFilter) ? _dateTimeFilter : DateTimeOffset.MinValue;

            // Place some restrictions on label filter, similar to the .net core provider.
            // The idea is to restrict queries to one label, and one label only. Even if that
            // one label is the "empty" label. Doing so will remove the decision making process
            // from this builders hands about which key/value/label tuple to choose when there
            // are multiple.
            _labelFilter = _labelFilter ?? "";
            if (_labelFilter.Contains('*') || _labelFilter.Contains(','))
            {
                _labelFilter = "";
                throw new ArgumentException("The characters '*' and ',' are not supported in label filters.", labelFilterTag);
            }

            // Always allow 'connectionString' to override black magic. But we expect this to be null most of the time.
            _connectionString = UpdateConfigSettingWithAppSettings(connectionStringTag);
            if (String.IsNullOrWhiteSpace(_connectionString))
            {
                _connectionString = null;

                // Use MSI Connector instead.
                string uri = UpdateConfigSettingWithAppSettings(endpointTag);
                if (!String.IsNullOrWhiteSpace(uri))
                {
                    try
                    {
                        _endpoint = new Uri(uri);
                        _client = AzconfigClientFactory.CreateClient(_endpoint, Permissions.Read).Result;
                    }
                    catch (Exception ex)
                    {
                        if (!Optional)
                            throw new ArgumentException($"Exception encountered while creating connection to Azure App Configuration store.", ex);
                    }
                    return;
                }

                throw new ArgumentException($"An endpoint URI or connection string must be provided for connecting to Azure App Configuration service via the '{endpointTag}' or '{connectionStringTag}' attributes.");
            }

            // If we get here, then we should try to connect with a connection string.
            try
            {
                _client = new AzconfigClient(_connectionString);
            }
            catch (Exception e) when (Optional && ((e.InnerException is System.Net.Http.HttpRequestException) || (e.InnerException is UnauthorizedAccessException))) { }
        }

        /// <summary>
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public override bool ValidateKey(string key)
        {
            // Azure App Config does not restrict key names, although a couple characters have special meaning if not escaped.
            // We may want to restrict using those characters unescaped in a key name in the future.
            return true;
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
        /// Retrieves all known key/value pairs from the Azure App Config store where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (_client == null)
                return data;

            QueryKeyValueCollectionOptions options = new QueryKeyValueCollectionOptions();
            options.LabelFilter = _labelFilter;
            options.FieldsSelector = KeyValueFields.Key | KeyValueFields.Value;
            if (!String.IsNullOrWhiteSpace(_keyFilter))
            {
                options.KeyFilter = _keyFilter;
            }
            if (_dateTimeFilter > DateTimeOffset.MinValue)
            {
                options.PreferredDateTime = _dateTimeFilter;
            }

            // We don't make any guarantees about which kv get precendence when there are multiple of the same key...
            // But the config service does seem to return kvs in a preferred order - no label first, then alphabetical by label.
            // Prefer the first kv we encounter from the config service.
            try
            {
                _client.GetKeyValues(options).Where(kv => kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ForEach(kv => {
                    if (!data.ContainsKey(kv.Key)) data[kv.Key] = kv.Value;
                });
            }
            catch (Exception e) when (Optional && ((e.InnerException is System.Net.Http.HttpRequestException) || (e.InnerException is UnauthorizedAccessException))) { }

            return data;
        }

        private async Task<string> GetValueAsync(string key)
        {
            if (_client == null)
                return null;

            QueryKeyValueOptions options = new QueryKeyValueOptions();
            options.Label = _labelFilter;
            options.FieldsSelector = KeyValueFields.Key | KeyValueFields.Value;
            if (_dateTimeFilter > DateTimeOffset.MinValue)
            {
                options.PreferredDateTime = _dateTimeFilter;
            }

            try
            {
                IKeyValue keyValue = await _client.GetKeyValue(key, options, CancellationToken.None);

                return keyValue?.Value;
            }
            catch (Exception e) when (Optional && ((e.InnerException is System.Net.Http.HttpRequestException) || (e.InnerException is UnauthorizedAccessException))) { }

            return null;
        }
    }
}
