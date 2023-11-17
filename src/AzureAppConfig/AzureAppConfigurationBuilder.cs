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
using Azure.Core;
using Azure.Data.AppConfiguration;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

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
        public const string dateTimeFilterTag = "acceptDateTime";
        public const string useKeyVaultTag = "useAzureKeyVault";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        /// <summary>
        /// Gets or sets the Uri of the config store to connect to.
        /// </summary>
        public string Endpoint { get; protected set; }

        /// <summary>
        /// Alternative to the preferred <see cref="Endpoint"/>, gets or sets a connection string used to connect to the config store.
        /// </summary>
        public string ConnectionString { get; protected set; }

        /// <summary>
        /// Gets or sets a 'Key Filter' to use when searching for config values.
        /// </summary>
        public string KeyFilter { get; protected set; }

        /// <summary>
        /// Gets or sets a 'Label Filter' to restrict the set of config values searched.
        /// </summary>
        public string LabelFilter { get; protected set; }

        /// <summary>
        /// Gets or sets a 'DateTime Filter' to query for config state as it existed at the given time.
        /// </summary>
        public DateTimeOffset AcceptDateTime { get; protected set; }

        /// <summary>
        /// Specifies whether this builder is allowed to connect to Azure Key Vault for chained secret lookup. (Default: false)
        /// </summary>
        public bool UseAzureKeyVault { get; protected set; } = false;

        private ConcurrentDictionary<Uri, SecretClient> _kvClientCache;
        private ConfigurationClient _client;

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            // Default to 'Enabled'. base.LazyInitialize() will override if specified in config.
            Enabled = KeyValueEnabled.Enabled;

            base.LazyInitialize(name, config);

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (Enabled == KeyValueEnabled.Disabled) return;

            // keyFilter
            KeyFilter = UpdateConfigSettingWithAppSettings(keyFilterTag);
            if (String.IsNullOrWhiteSpace(KeyFilter))
                KeyFilter = null;

            // labelFilter
            // Place some restrictions on label filter, similar to the .net core provider.
            // The idea is to restrict queries to one label, and one label only. Even if that
            // one label is the "empty" label. Doing so will remove the decision making process
            // from this builders hands about which key/value/label tuple to choose when there
            // are multiple.
            LabelFilter = UpdateConfigSettingWithAppSettings(labelFilterTag);
            if (String.IsNullOrWhiteSpace(LabelFilter)) {
                LabelFilter = null;
            }
            else if (LabelFilter.Contains('*') || LabelFilter.Contains(',')) {
                throw new ArgumentException("The characters '*' and ',' are not supported in label filters.", labelFilterTag);
            }

            // acceptDateTime
            AcceptDateTime = (UpdateConfigSettingWithAppSettings(dateTimeFilterTag) != null) ? DateTimeOffset.Parse(config[dateTimeFilterTag]) : AcceptDateTime;

            // Azure Key Vault Integration
            UseAzureKeyVault = (UpdateConfigSettingWithAppSettings(useKeyVaultTag) != null) ? Boolean.Parse(config[useKeyVaultTag]) : UseAzureKeyVault;
            if (UseAzureKeyVault)
                _kvClientCache = new ConcurrentDictionary<Uri, SecretClient>(EqualityComparer<Uri>.Default);


            // Moving to align with other Azure builders, rely on Azure Identities before connection strings
            Endpoint = UpdateConfigSettingWithAppSettings(endpointTag);
            if (!String.IsNullOrWhiteSpace(Endpoint))
            {
                try
                {
                    var uri = new Uri(Endpoint);
                    _client = new ConfigurationClient(uri, GetCredential(), GetConfigurationClientOptions());
                }
                catch (Exception ex)
                {
                    if (!IsOptional)
                        throw new ArgumentException($"Exception encountered while creating connection to Azure App Configuration store.", ex);
                }
            }
            // Don't fall back on connection string unless endpoint was not even specified.
            else
            {
                ConnectionString = UpdateConfigSettingWithAppSettings(connectionStringTag);
                if (!String.IsNullOrWhiteSpace(ConnectionString))
                {
                    try
                    {
                        _client = new ConfigurationClient(ConnectionString);
                    }
                    catch (Exception ex)
                    {
                        if (!IsOptional)
                            throw new ArgumentException($"Exception encountered while creating connection to Azure App Configuration store.", ex);
                    }
                }
                else
                {
                    // Getting here means neither endpoint nor connectionString were given
                    throw new ArgumentException($"An endpoint URI or connection string must be provided for connecting to Azure App Configuration service via the '{endpointTag}' or '{connectionStringTag}' attribute.");
                }
            }

            // At this point we've got all our ducks in a row and are ready to go. And we know that
            // we will be used, because this is the 'lazy' initializer. But let's handle one oddball case
            // before we go.
            // If we have a keyFilter set, then we will always query a set of values instead of a single
            // value, regardless of whether we are in strict/token/greedy mode. But if we're not in
            // greedy mode, then the base KeyValueConfigBuilder will still request each key/value it is
            // interested in one at a time, and only cache that one result. So we will end up querying the
            // same set of values from the AppConfig service for every value. Let's only do this once and
            // cache the entire set to make those calls to GetValueInternal read from the cache instead of
            // hitting the service every time.
            if (KeyFilter != null && Mode != KeyValueMode.Greedy)
                EnsureGreedyInitialized();
        }

        /// <summary>
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public override bool ValidateKey(string key)
        {
            // From - https://docs.microsoft.com/en-us/azure/azure-app-configuration/concept-key-value
            // You can use any unicode character in key names entered into App Configuration except for *, ,, and \. These characters are
            // reserved.If you need to include a reserved character, you must escape it by using \{ Reserved Character}.
            if (String.IsNullOrWhiteSpace(key))
                return false;

            if (key.Contains('*') || key.Contains(','))
                return false;

            // We don't want to completely disallow '\' since it is used for escaping. But writing a full parser for someone elses
            // naming format could be error prone. If we see a '\' followed by a '{', just call it good. Don't bother with the Regex
            // if there aren't any backslashes though.
            if (key.Contains('\\'))
                return !Regex.IsMatch(key, @"\\[^{]");

            return true;
        }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' for the secret to look up in the configured Key Vault. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            // Quick shortcut. If we have a keyFilter set, then we've already populated the cache with
            // all possible values for this builder. If we get here, that means the key was not found in
            // the cache. Going further will query with just the key name, and no keyFilter applied. This
            // could result in finding a value... but we shouldn't, because the requested key does not
            // match the keyFilter - otherwise it would already be in the cache. Avoid the trouble and
            // shortcut return nothing in this case.
            if (KeyFilter != null)
                return null;

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
            // This is also a synchronous method. And in single-threaded contexts like ASP.Net
            // it can be bad/dangerous to block on async calls. So lets work some TPL voodoo
            // again to avoid potential deadlocks.
            return Task.Run(async () => { return await GetAllValuesAsync(prefix); }).Result;
        }

        /// <summary>
        /// Gets a <see cref="TokenCredential"/> to authenticate with App Configuration including Key-Value references to Key Vault. This defaults to <see cref="DefaultAzureCredential"/>.
        /// </summary>
        /// <returns>A token credential.</returns>
        protected virtual TokenCredential GetCredential() => new DefaultAzureCredential();

        /// <summary>
        /// Gets a <see cref="SecretClientOptions"/> to initialize the Key Vault SecretClient with. This defaults to a new <see cref="SecretClientOptions"/>.
        /// </summary>
        /// <returns>A token credential.</returns>
        protected virtual SecretClientOptions GetSecretClientOptions() => new SecretClientOptions();

        /// <summary>
        /// Gets a <see cref="ConfigurationClientOptions"/> to initialize the Key Vault SecretClient with. This defaults to a new <see cref="ConfigurationClientOptions"/>.
        /// </summary>
        /// <returns>A token credential.</returns>
        protected virtual ConfigurationClientOptions GetConfigurationClientOptions() => new ConfigurationClientOptions();

        private async Task<string> GetValueAsync(string key)
        {
            if (_client == null)
                return null;

            SettingSelector selector = new SettingSelector { KeyFilter = key };

            if (LabelFilter != null)
            {
                selector.LabelFilter = LabelFilter;
            }
            if (AcceptDateTime > DateTimeOffset.MinValue)
            {
                selector.AcceptDateTime = AcceptDateTime;
            }
            // TODO: Reduce bandwidth by limiting the fields we retrieve.
            // Currently, content type doesn't get delivered, even if we add it to the selection. This prevents KeyVault recognition.
            //selector.Fields = SettingFields.Key | SettingFields.Value | SettingFields.ContentType;

            try
            {
                AsyncPageable<ConfigurationSetting> settings = _client.GetConfigurationSettingsAsync(selector);
                IAsyncEnumerator<ConfigurationSetting> enumerator = settings.GetAsyncEnumerator();

                try
                {
                    // There should only be one result. If there's more, we're only returning the fisrt.
                    await enumerator.MoveNextAsync();
                    ConfigurationSetting current = enumerator.Current;
                    if (current == null)
                        return null;

                    if (UseAzureKeyVault && current is SecretReferenceConfigurationSetting secretReference)
                    {
                        try
                        {
                            return await GetKeyVaultValue(secretReference);
                        }
                        catch (Exception)
                        {
                            // 'Optional' plays a double role with this provider. Being optional means it is
                            // ok for us to fail to resolve a keyvault reference. If we are not optional though,
                            // we want to make some noise when a reference fails to resolve.
                            if (!IsOptional)
                                throw;
                        }
                    }

                    return current.Value;
                }
                finally
                {
                    await enumerator.DisposeAsync();
                }
            }
            catch (Exception e) when (IsOptional && ((e.InnerException is System.Net.Http.HttpRequestException) || (e.InnerException is UnauthorizedAccessException))) { }

            return null;
        }

        private async Task<ICollection<KeyValuePair<string, string>>> GetAllValuesAsync(string prefix)
        {
            Dictionary<string, string> data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (_client == null)
                return data;

            SettingSelector selector = new SettingSelector();
            if (KeyFilter != null)
            {
                selector.KeyFilter = KeyFilter;
            }
            if (LabelFilter != null)
            {
                selector.LabelFilter = LabelFilter;
            }
            if (AcceptDateTime > DateTimeOffset.MinValue)
            {
                selector.AcceptDateTime = AcceptDateTime;
            }
            // TODO: Reduce bandwidth by limiting the fields we retrieve.
            // Currently, content type doesn't get delivered, even if we add it to the selection. This prevents KeyVault recognition.
            //selector.Fields = SettingFields.Key | SettingFields.Value | SettingFields.ContentType;

            // We don't make any guarantees about which kv get precendence when there are multiple of the same key...
            // But the config service does seem to return kvs in a preferred order - no label first, then alphabetical by label.
            // Prefer the first kv we encounter from the config service.
            try
            {
                AsyncPageable<ConfigurationSetting> settings = _client.GetConfigurationSettingsAsync(selector);
                IAsyncEnumerator<ConfigurationSetting> enumerator = settings.GetAsyncEnumerator();
                try
                {
                    while (await enumerator.MoveNextAsync())
                    {
                        ConfigurationSetting setting = enumerator.Current;
                        string configValue = setting.Value;

                        // Move on to the next if the prefix doesn't match
                        if (!String.IsNullOrEmpty(prefix) && !setting.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                            continue;

                        // If it's a key vault reference, go fetch the value from key vault
                        if (UseAzureKeyVault && setting is SecretReferenceConfigurationSetting secretReference)
                        {
                            try
                            {
                                configValue = await GetKeyVaultValue(secretReference);
                            }
                            catch (Exception)
                            {
                                // 'Optional' plays a double role with this provider. Being optional means it is
                                // ok for us to fail to resolve a keyvault reference. If we are not optional though,
                                // we want to make some noise when a reference fails to resolve.
                                if (!IsOptional)
                                    throw;
                            }
                        }

                        if (!data.ContainsKey(setting.Key))
                            data[setting.Key] = configValue;
                    }
                }
                finally
                {
                    if (enumerator != null)
                        await enumerator.DisposeAsync();
                }
            }
            catch (Exception e) when (IsOptional && ((e.InnerException is System.Net.Http.HttpRequestException) || (e.InnerException is UnauthorizedAccessException))) { }

            return data;
        }

        private async Task<string> GetKeyVaultValue(SecretReferenceConfigurationSetting secretReference)
        {
            KeyVaultSecretIdentifier secretIdentifier = new KeyVaultSecretIdentifier(secretReference.SecretId);
            SecretClient kvClient = GetSecretClient(secretIdentifier);
            if (kvClient == null && !IsOptional)
                throw new RequestFailedException("Could not connect to Azure Key Vault while retrieving secret. Connection is not optional.");

            // Retrieve Value
            Response<KeyVaultSecret> resp = await kvClient.GetSecretAsync(secretIdentifier.Name, secretIdentifier.Version);
            KeyVaultSecret kvSecret = resp.Value;
            if (kvSecret != null && kvSecret.Properties.Enabled.GetValueOrDefault())
                return kvSecret.Value;

            return null;
        }

        private SecretClient GetSecretClient(KeyVaultSecretIdentifier identifier)
        {
            return _kvClientCache.GetOrAdd(identifier.VaultUri, uri => new SecretClient(identifier.VaultUri, GetCredential(), GetSecretClientOptions()));
        }
    }
}
