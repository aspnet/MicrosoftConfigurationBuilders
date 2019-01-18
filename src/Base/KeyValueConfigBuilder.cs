// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Configuration;
using System.Collections.Concurrent;
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
        public const string optionalTag = "optional";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private NameValueCollection _config = null;
        private IDictionary<string, string> _cachedValues;
        private bool _lazyInitialized = false;
        private bool _greedyInitialized = false;

        /// <summary>
        /// Gets or sets the substitution pattern to be used by the KeyValueConfigBuilder.
        /// </summary>
        public KeyValueMode Mode { get; private set; } = KeyValueMode.Strict;
        /// <summary>
        /// Gets or sets a prefix string that must be matched by keys to be considered for value substitution.
        /// </summary>
        public string KeyPrefix { get { EnsureInitialized(); return _keyPrefix; } }
        private string _keyPrefix = "";
        private bool StripPrefix { get { EnsureInitialized(); return _stripPrefix; } }
        private bool _stripPrefix = false;  // Prefix-stripping is all handled in this base class; this is private so it doesn't confuse sub-classes.
        /// <summary>
        /// Specifies whether the config builder should cause errors if the backing source cannot be found.
        /// </summary>
        public bool Optional { get { EnsureInitialized(); return _optional; } protected set { _optional = value; } }
        private bool _optional = true;
        /// <summary>
        /// Gets or sets a regular expression used for matching tokens in raw xml during Greedy substitution.
        /// </summary>
        public string TokenPattern { get { EnsureInitialized(); return _tokenPattern; } protected set { _tokenPattern = value; } }
        private string _tokenPattern = @"\$\{(\w+)\}";

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
        /// Makes a determination about whether the input key is valid for this builder and backing store.
        /// </summary>
        /// <param name="key">The string to be validated. May be partial.</param>
        /// <returns>True if the string is valid. False if the string is not a valid key.</returns>
        public virtual bool ValidateKey(string key) { return true; }
        /// <summary>
        /// Transforms the raw key read from the config file to a new string when updating items in Strict and Greedy modes.
        /// </summary>
        /// <param name="rawKey">The key as read from the incomming config section.</param>
        /// <returns>The key string that will be left in the processed config section.</returns>
        public virtual string UpdateKey(string rawKey) { return rawKey; }

        /// <summary>
        /// Initializes the configuration builder.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);
            _config = config ?? new NameValueCollection();

            // Mode can't be lazy initialized, because it is used to determine how late we can go before initializing.
            // Reading it would force initialization too early in many cases.
            if (_config[modeTag] != null)
            {
                // We want an exception here if 'mode' is specified but unrecognized.
                Mode = (KeyValueMode)Enum.Parse(typeof(KeyValueMode), config[modeTag], true);
            }
        }

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected virtual void LazyInitialize(string name, NameValueCollection config)
        {
            // Use pre-assigned defaults if not specified. Non-freeform options should throw on unrecognized values.
            _keyPrefix = config[prefixTag] ?? _keyPrefix;
            _stripPrefix = (config[stripPrefixTag] != null) ? Boolean.Parse(config[stripPrefixTag]) : _stripPrefix;
            _optional = (config[optionalTag] != null) ? Boolean.Parse(config[optionalTag]) : _optional;
            _tokenPattern = config[tokenPatternTag] ?? _tokenPattern;

            _cachedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _lazyInitialized = true;
        }

        //=========================================================================================================================
        #region "Private" stuff
        // Sub-classes need not worry about this stuff, even though some of it is "public" because it comes from the framework.

#pragma warning disable CS1591 // No xml comments for overrides that implementing classes shouldn't worry about.
        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            if (Mode == KeyValueMode.Expand)
                return ExpandTokens(rawXml);

            return rawXml;
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            // Expand mode only works on the raw string input
            if (Mode == KeyValueMode.Expand)
                return configSection;

            // See if we know how to process this section
            ISectionHandler handler = SectionHandlersSection.GetSectionHandler(configSection);
            if (handler == null)
                return configSection;

            // Strict Mode. Only replace existing key/values.
            if (Mode == KeyValueMode.Strict)
            {
                foreach (var configItem in handler)
                {
                    string newValue = GetValueInternal(configItem.Key);
                    string newKey = UpdateKey(configItem.Key);

                    if (newValue != null)
                        handler.InsertOrUpdate(newKey, newValue, configItem.Key, configItem.Value);
                }
            }

            // Greedy Mode. Insert all key/values.
            else if (Mode == KeyValueMode.Greedy)
            {
                EnsureGreedyInitialized();
                foreach (KeyValuePair<string, string> kvp in _cachedValues)
                {
                    if (kvp.Value != null)
                    {
                        string oldKey = TrimPrefix(kvp.Key);
                        string newKey = UpdateKey(oldKey);
                        handler.InsertOrUpdate(newKey, kvp.Value, oldKey);
                    }
                }
            }

            return configSection;
        }
        #pragma warning restore CS1591 // No xml comments for overrides that implementing classes shouldn't worry about.

        private void EnsureInitialized()
        {
            if (!_lazyInitialized)
            {
                lock (this)
                {
                    if (!_lazyInitialized)
                    {
                        LazyInitialize(Name, _config);
                    }
                }
            }
        }

        private void EnsureGreedyInitialized()
        {
            try
            {
                // In Greedy mode, we need to know all the key/value pairs from this config source. So we
                // can't 'cache' them as we go along. Slurp them all up now. But only once. ;)
                if (!_greedyInitialized && ValidateKey(KeyPrefix))
                {
                    lock (_cachedValues)
                    {
                        if (!_greedyInitialized)
                        {
                            foreach (KeyValuePair<string, string> kvp in GetAllValues(KeyPrefix))
                            {
                                _cachedValues.Add(kvp);
                            }
                            _greedyInitialized = true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error in Configuration Builder '{Name}'::GetAllValues({KeyPrefix})", e);
            }
        }

        private XmlNode ExpandTokens(XmlNode rawXml)
        {
            string rawXmlString = rawXml.OuterXml;

            if (String.IsNullOrEmpty(rawXmlString))
                return rawXml;

            rawXmlString = Regex.Replace(rawXmlString, TokenPattern, (m) =>
                {
                    string key = m.Groups[1].Value;

                    // Same prefix-handling rules apply in expand mode as in strict mode.
                    // Since the key is being completely replaced by the value, we don't need to call UpdateKey().
                    return GetValueInternal(key) ?? m.Groups[0].Value;
                });
            
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.LoadXml(rawXmlString);
            return doc.DocumentElement;
        }

        private string GetValueInternal(string key)
        {
            if (String.IsNullOrEmpty(key))
                return null;

            try
            {
                // Make sure the key we are looking up begins with the correct prefix... if we are not stripping prefixes.
                if (!StripPrefix && !key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                    return null;

                // Stripping Prefix in strict mode means from the source key. The static config file will have a prefix-less key to match.
                // ie <add key="MySetting" /> should only match the key/value (KeyPrefix + "MySetting") from the source.
                string sourceKey = (StripPrefix) ? KeyPrefix + key : key;

                if (!ValidateKey(sourceKey))
                    return null;

                return (_cachedValues.ContainsKey(sourceKey)) ? _cachedValues[sourceKey] : _cachedValues[sourceKey] = GetValue(sourceKey);
            }
            catch (Exception e)
            {
                throw new Exception($"Error in Configuration Builder '{Name}'::GetValue({key})", e);
            }

            return null;
        }

        private string TrimPrefix(string fullString)
        {
            if (!StripPrefix || !fullString.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase))
                return fullString;

            return fullString.Substring(KeyPrefix.Length);
        }

        #endregion
        //=========================================================================================================================
    }
}
