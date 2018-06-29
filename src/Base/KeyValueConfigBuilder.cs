// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// Base class for a set of ConfigurationBuilders that follow a simple key/value pair substitution model. This base
    /// class handles substitution modes and most prefix concerns, so implementing classes only need to be a simple
    /// source of key/value pairs through the <see cref="GetValue(string)"/> and <see cref="GetAllValues(string)"/> methods.
    /// </summary>
    public abstract class KeyValueConfigBuilder : ConfigurationBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string modeTag = "mode";
        public const string prefixTag = "prefix";
        public const string stripPrefixTag = "stripPrefix";
        public const string tokenPatternTag = "tokenPattern";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private bool _greedyInited;
        private IDictionary<string, string> _cachedValues;
        private bool _stripPrefix = false;  // Prefix-stripping is all handled in this class; this is private so it doesn't confuse sub-classes.

        /// <summary>
        /// Gets or sets a regular expression used for matching tokens in raw xml during Greedy substitution.
        /// </summary>
        public string TokenPattern { get; protected set; } = @"\$\{(\w+)\}";
        /// <summary>
        /// Gets or sets the substitution pattern to be used by the KeyValueConfigBuilder.
        /// </summary>
        public KeyValueMode Mode { get; private set; } = KeyValueMode.Strict;
        /// <summary>
        /// Gets or sets a prefix string that must be matched by keys to be considered for value substitution.
        /// </summary>
        public string KeyPrefix { get; private set; }

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' to look up in the config source. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public abstract string GetValue(string key);
        /// <summary>
        /// Retrieves all known key/value pairs for the configuration source where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public abstract ICollection<KeyValuePair<string, string>> GetAllValues(string prefix);

        /// <summary>
        /// Initializes the configuration builder.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            // Override default config
            if (config != null)
            {
                KeyPrefix = config[prefixTag] ?? "";
                TokenPattern = config[tokenPatternTag] ?? TokenPattern;

                if (config[stripPrefixTag] != null) {
                    // We want an exception here if 'stripPrefix' is specified but unrecognized.
                    _stripPrefix = Boolean.Parse(config[stripPrefixTag]);
                }

                if (config[modeTag] != null) {
                    // We want an exception here if 'mode' is specified but unrecognized.
                    Mode = (KeyValueMode)Enum.Parse(typeof(KeyValueMode), config[modeTag], true);
                }
            }

            _cachedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        #pragma warning disable CS1591 // No xml comments for overrides that implementing classes shouldn't worry about.
        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            if (Mode == KeyValueMode.Expand)
                return ExpandTokens(rawXml);

            return rawXml;
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            // Expand mode works on the raw string input
            if (Mode == KeyValueMode.Expand)
                return configSection;

            // In Greedy mode, we need to know all the key/value pairs from this config source. So we
            // can't 'cache' them as we go along. Slurp them all up now. But only once. ;)
            if ((Mode == KeyValueMode.Greedy) && (!_greedyInited))
            {
                lock (_cachedValues)
                {
                    if (!_greedyInited)
                    {
                        foreach (KeyValuePair<string, string> kvp in GetAllValuesInternal(KeyPrefix))
                        {
                            _cachedValues.Add(kvp);
                        }
                        _greedyInited = true;
                    }
                }
            }

            if (configSection is AppSettingsSection) {
                return ProcessAppSettings((AppSettingsSection)configSection);
            }
            else if (configSection is ConnectionStringsSection) {
                return ProcessConnectionStrings((ConnectionStringsSection)configSection);
            }

            return configSection;
        }
        #pragma warning restore CS1591 // No xml comments for overrides that implementing classes shouldn't worry about.

        private XmlNode ExpandTokens(XmlNode rawXml)
        {
            string rawXmlString = rawXml.OuterXml;

            if (String.IsNullOrEmpty(rawXmlString))
                return rawXml;

            rawXmlString = Regex.Replace(rawXmlString, TokenPattern, (m) =>
                {
                    string key = m.Groups[1].Value;

                    // Same prefix-handling rules apply in expand mode as in strict mode.
                    return ProcessKeyStrict(key, (k, v) => {
                        if (v != null)
                            return v;
                        return m.Groups[0].Value;
                    });
                });
            
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(rawXmlString);
            return doc.DocumentElement;
        }

        private AppSettingsSection ProcessAppSettings(AppSettingsSection appSettings)
        {
            if (appSettings != null) {
                // Strict Mode. Only replace existing key/values.
                if (Mode == KeyValueMode.Strict) {
                    foreach (string key in appSettings.Settings.AllKeys) {
                        ProcessKeyStrict(key, (k, v) => {
                            if (v != null)
                            {
                                appSettings.Settings.Remove(k);
                                appSettings.Settings.Add(k, v);
                            }
                        });
                    }
                }

                // Greedy Mode. Insert all key/values.
                else if (Mode == KeyValueMode.Greedy) {
                    foreach (KeyValuePair<string, string> kvp in _cachedValues) {
                        if (kvp.Value != null) {
                            string strippedKey = TrimPrefix(kvp.Key);
                            appSettings.Settings.Remove(strippedKey);
                            appSettings.Settings.Add(strippedKey, kvp.Value);
                        }
                    }
                }
            }

            return appSettings;
        }

        private ConnectionStringsSection ProcessConnectionStrings(ConnectionStringsSection connStrings)
        {
            if (connStrings != null) {
                // Strict Mode. Only replace existing key/values.
                if (Mode == KeyValueMode.Strict) {
                    foreach (ConnectionStringSettings cs in connStrings.ConnectionStrings) {
                        ProcessKeyStrict(cs.Name, (k, v) => {
                            cs.Name = k;
                            cs.ConnectionString = v ?? cs.ConnectionString;
                        });
                    }
                }

                // Greedy Mode. Insert all key/values.
                else if (Mode == KeyValueMode.Greedy) {
                    foreach (KeyValuePair<string, string> kvp in _cachedValues) {
                        if (kvp.Value != null) {
                            string strippedKey = TrimPrefix(kvp.Key);
                            ConnectionStringSettings cs = connStrings.ConnectionStrings[kvp.Key] ?? new ConnectionStringSettings();
                            connStrings.ConnectionStrings.Remove(strippedKey);
                            cs.Name = strippedKey;
                            cs.ConnectionString = kvp.Value;
                            connStrings.ConnectionStrings.Add(cs);
                        }
                    }
                }
            }

            return connStrings;
        }

        private void ProcessKeyStrict(string key, Action<string, string> replaceAction)
        {
            ProcessKeyStrict(key, (k, v) => { replaceAction(k, v); return null; });
        }
        private string ProcessKeyStrict(string key, Func<string, string, string> replaceAction)
        {
            if (_stripPrefix)
            {
                // Stripping Prefix in strict mode means from the source key. The static config file will have a prefix-less key to match.
                // ie <add key="MySetting" /> should only match the key/value (KeyPrefix + "MySetting") from the source.
                string sourceKey = KeyPrefix + key;
                string value = (_cachedValues.ContainsKey(sourceKey)) ? _cachedValues[sourceKey] : _cachedValues[sourceKey] = GetValueInternal(sourceKey);
                return replaceAction(key, value);
            }
            else
            {
                // Not stripping Prefix in strict mode means the source and static config keys will match exactly, and they will both begin
                // with the prefix.
                if (key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = (_cachedValues.ContainsKey(key)) ? _cachedValues[key] : _cachedValues[key] = GetValueInternal(key);
                    return replaceAction(key, value);
                }
            }

            return replaceAction(key, null);
        }

        private string TrimPrefix(string fullString)
        {
            if (!_stripPrefix || !fullString.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                return fullString;

            return fullString.Substring(KeyPrefix.Length);
        }

        private string GetValueInternal(string key)
        {
            if (String.IsNullOrEmpty(key)) { return null; }

            try
            {
                return GetValue(key);
            }
            catch (Exception e)
            {
                throw new Exception($"Error in Configuration Builder '{Name}'::GetValue({key})", e);
            }
        }

        private ICollection<KeyValuePair<string, string>> GetAllValuesInternal(string prefix)
        {
            try
            {
                return GetAllValues(prefix);
            }
            catch (Exception e)
            {
                throw new Exception($"Error in Configuration Builder '{Name}'::GetAllValues({prefix})", e);
            }
        }
    }
}
