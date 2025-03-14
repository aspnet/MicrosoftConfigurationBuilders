// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime;
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
        public const string snapshotTag = "snapshot";
        public const string keyFilterTag = "keyFilter";
        public const string labelFilterTag = "labelFilter";
        public const string dateTimeFilterTag = "acceptDateTime";
        public const string useKeyVaultTag = "useAzureKeyVault";
        public const string preloadTag = "preloadValues";
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
        /// Gets or sets the name of a Snapshot to retrieve config values from.
        /// If provided, this setting supercedes the Label, Key, and DateTime filters.
        /// </summary>
        public string Snapshot { get; protected set; }

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

        /// <summary>
        /// Specifies whether the config builder should load all (filtered) config values from the store up front, or
        /// load them all individually when needed. (Does not apply to Greedy mode, which preloads all values automatically.)
        /// This can greatly reduce the number of calls to the config store to help avoid throttling.
        /// </summary>
        public bool PreloadValues { get; protected set; } = true;

        private ConcurrentDictionary<Uri, SecretClient> _kvClientCache;
        private ConfigurationClient _client;
        private FieldInfo _cachedValuesField;
        private bool _valuesPreloaded = false;

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

            // TODO: This is super hacky. In a major-version update, this should be revamped in cooperation with the KVCB base class.
            // When we cache our values, we drew them from a source where case matters. Case should still matter in our cache.
            // Replace the built-in case-insensitive cache with a case-sensitive one.
            _cachedValuesField = typeof(KeyValueConfigBuilder).GetField("_cachedValues", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_cachedValuesField != null)
                _cachedValuesField.SetValue(this, new Dictionary<string, string>());

            // At this point, we have our 'Enabled' choice. If we are disabled, we can stop right here.
            if (Enabled == KeyValueEnabled.Disabled) return;

            // snapshot
            Snapshot = UpdateConfigSettingWithAppSettings(snapshotTag);
            if (String.IsNullOrWhiteSpace(Snapshot))
            {
                // Only configure other filters if 'snapshot' is not provided.
                Snapshot = null;

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
            }

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

            // Preload - Enabled if explicity set to true in the config, or if Snapshot or KeyFilter are used.
            // Cannot be disabled explicitly in the case of Snapshot or KeyFilter. Be noisy about that.
            // Does not make sense in Greedy mode, but no need to make noise about that.
            if (Mode == KeyValueMode.Greedy)
            {
                PreloadValues = true;   // Just say true and be done with it.
            }
            else
            {
                // Implicitly, use default preload value - unless Snapshot or KeyFilter are used, then preload is true.
                var implicitPreload = (Snapshot != null || KeyFilter != null);
                PreloadValues |= implicitPreload;

                // However, if preload is explicitly set to true in the config, check to see if the configured
                // value is compatible with the current settings.
                if ((UpdateConfigSettingWithAppSettings(preloadTag) != null))
                {
                    var explicitPreload = Boolean.Parse(config[preloadTag]);

                    // Cannot explicitly set preload to false if using snapshot or keyFilter.
                    if (implicitPreload && !explicitPreload)
                        throw new ArgumentException($"The '{preloadTag}' attribute cannot be set to false when using the '{snapshotTag}' or '{keyFilterTag}' attributes.");

                    // If there is no conflict, use the explicitly requested value.
                    PreloadValues = explicitPreload;
                }
            }
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
            // Special case - Preload
            // If we are to preload values, we need to make sure that we have already done so.
            // If we haven't, then we need to do that now.
            // If we have though, then the base KVCB has already checked the cache and did not
            // find what it needed. Going back to the store yet again is likely to be a waste of time.
            // Avoid the trouble and shortcut return nothing in this case.
            if (PreloadValues)
            {
                if (!_valuesPreloaded)
                {
                    string retval = null;
                    if (_cachedValuesField != null)
                    {
                        var cachedValues = _cachedValuesField.GetValue(this) as Dictionary<string, string>;
                        foreach (var kvp in GetAllValues(null))
                            cachedValues.Add(kvp.Key, kvp.Value);
                        cachedValues.TryGetValue(key, out retval);
                    }
                    _valuesPreloaded = true;
                    return retval;
                }
                return null;
            }

            // This is a synchronous method. And in single-threaded contexts like ASP.Net
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
        /// Gets a <see cref="TokenCredential"/> to authenticate with App Configuration including Key-Value references to Azure Key Vault. This defaults to <see cref="DefaultAzureCredential"/>.
        /// </summary>
        /// <returns>A token credential.</returns>
        protected virtual TokenCredential GetCredential() => new DefaultAzureCredential();

        /// <summary>
        /// Gets a <see cref="SecretClientOptions"/> to initialize the Key Vault <see cref="SecretClient"/> with. This defaults to a new <see cref="SecretClientOptions"/>.
        /// </summary>
        /// <returns>A <see cref="SecretClientOptions"/> instance.</returns>
        /// <remarks>The <see cref="SecretClient"/> is used here to read Azure App Configuration key-value references to Azure Key Vault.</remarks>
        protected virtual SecretClientOptions GetSecretClientOptions() => new SecretClientOptions();

        /// <summary>
        /// Gets a <see cref="ConfigurationClientOptions"/> to initialize <see cref="ConfigurationClient"/> with. This defaults to a new <see cref="ConfigurationClientOptions"/>.
        /// </summary>
        /// <returns>A <see cref="ConfigurationClientOptions"/> instance.</returns>
        protected virtual ConfigurationClientOptions GetConfigurationClientOptions() => new ConfigurationClientOptions();

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
                // There was an error connecting over the web. DNS, timeout, etc.
                if (rfex.InnerException is System.Net.WebException we)
                    return IsOptional;

                // ConfigClient will not throw this exception for not-found values.
                // But resolving KeyVault config references still might encounter these cases.

                // Secret wasn't found - This is ok at all times. It just means we asked for a value
                // and Key Vault doesn't have it. Move along.
                if (rfex.Status == 404 || rfex.Status == 400)
                    return true;

                // Access was denied
                if (rfex.Status == 403)
                    return IsOptional;
            }

            // All Auth exceptions are potentially optional
            if (e is AuthenticationRequiredException || e is AuthenticationFailedException || e is CredentialUnavailableException)
                return IsOptional;

            // Even when 'optional', don't catch things unless we're certain we know what it is.
            return false;
        }

        private async Task<string> GetValueAsync(string key)
        {
            if (_client == null)
                return null;

            try
            {
                ConfigurationSetting setting = await GetConfigSettingAsync(key);

                if (setting == null)
                    return null;

                if (UseAzureKeyVault && setting is SecretReferenceConfigurationSetting secretReference)
                {
                    return await GetKeyVaultValue(secretReference);
                }

                return setting.Value;
            }
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return null;
        }

        private async Task<ICollection<KeyValuePair<string, string>>> GetAllValuesAsync(string prefix)
        {
            // This should be case-sensitive, as the config service is case-sensitive.
            // When doing prefix comparisons however, do those as case-insensitive, since the
            // expectation is that prefixes from the builder definition in config are case-insensitive.
            Dictionary<string, string> data = new Dictionary<string, string>();

            if (_client == null)
                return data;

            // We don't make any guarantees about which kv get precendence when there are multiple of the same key...
            // But the config service does seem to return kvs in a preferred order - no label first, then alphabetical by label.
            // Prefer the first kv we encounter from the config service.
            try
            {
                AsyncPageable<ConfigurationSetting> settings = GetConfigSettings();
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
                            catch (AggregateException ae)
                            {
                                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
                            }
                            catch (Exception e) when (ExceptionIsOptional(e)) { }
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
            catch (AggregateException ae)
            {
                ae.Handle((ex) => ExceptionIsOptional(ex)); // Re-throws if not optional
            }
            catch (Exception e) when (ExceptionIsOptional(e)) { }

            return data;
        }

        private async Task<ConfigurationSetting> GetConfigSettingAsync(string name)
        {
            AsyncPageable<ConfigurationSetting> settings = GetConfigSettings(name);

            // Alas, 'await using' isn't available in C# 7.3, which is technically the last supported C# for .NET Framework.
            IAsyncEnumerator<ConfigurationSetting> enumerator = settings.GetAsyncEnumerator();
            try
            {
                // TODO smolloy - In Snapshot mode, there is no way to select just this key.
                // There should only be one result. If there's more, we're only returning the fisrt.
                await enumerator.MoveNextAsync();
                return enumerator.Current;
            }
            finally
            {
                await enumerator.DisposeAsync();
            }
        }

        private AsyncPageable<ConfigurationSetting> GetConfigSettings(string name = null)
        {
            // TODO: Reduce bandwidth by limiting the fields we retrieve.
            // Currently, content type doesn't get delivered, even if we add it to the selection. This prevents KeyVault recognition.
            //selector.Fields = SettingFields.Key | SettingFields.Value | SettingFields.ContentType;

            // Use a snapshot if it was provided
            if (Snapshot != null)
            {
                return _client.GetConfigurationSettingsForSnapshotAsync(Snapshot);
            }
            else
            {
                SettingSelector selector = new SettingSelector();
                if (name != null)
                {
                    selector.KeyFilter = name;
                }
                else if (KeyFilter != null)
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

                // We use 'GetSetting_s_' here because the singular version doesn't support multiple filters. :/
                return _client.GetConfigurationSettingsAsync(selector);
            }
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
