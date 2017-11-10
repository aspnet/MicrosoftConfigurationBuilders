using System;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Xml;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public abstract class KeyValueConfigBuilder : ConfigurationBuilder
    {
        public const string modeTag = "mode";
        public const string prefixTag = "prefix";
        public const string stripPrefixTag = "stripPrefix";

        private bool _greedyInited;
        private IDictionary<string, string> _cachedValues;
        private bool _stripPrefix = false;  // Prefix-stripping is all handled in this class; this is private so it doesn't confuse sub-classes.

        public KeyValueMode Mode { get; private set; } = KeyValueMode.Strict;
        public string KeyPrefix { get; private set; }

        public abstract string GetValue(string key);
        public abstract ICollection<KeyValuePair<string, string>> GetAllValues(string prefix);

        public override void Initialize(string name, NameValueCollection config)
        {
            try
            {
                base.Initialize(name, config);

                KeyPrefix = config?[prefixTag] ?? "";

                if (config != null && config[stripPrefixTag] != null)
                {
                    // We want an exception here if 'stripPrefix' is specified but unrecognized.
                    _stripPrefix = Boolean.Parse(config[stripPrefixTag]);
                }

                if (config != null && config[modeTag] != null)
                {
                    // We want an exception here if 'mode' is specified but unrecognized.
                    Mode = (KeyValueMode)Enum.Parse(typeof(KeyValueMode), config[modeTag], true);
                }

                _cachedValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                throw new ConfigurationErrorsException($"Error while initializing Configuration Builder '{name}'. ({e.Message})", e);
            }
        }

        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            try
            {
                if (Mode == KeyValueMode.Expand)
                    return ExpandTokens(rawXml);

                return rawXml;
            }
            catch (Exception e)
            {
                throw new ConfigurationErrorsException($"Error while processing xml in Configuration Builder '{Name}'. ({e.Message})", e);
            }
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            try {
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
            catch (Exception e)
            {
                throw new ConfigurationErrorsException($"Error while processing configSection in Configuration Builder '{Name}'. ({e.Message})", e);
            }
        }

        private XmlNode ExpandTokens(XmlNode rawXml)
        {
            string rawXmlString = rawXml.OuterXml;

            if (String.IsNullOrEmpty(rawXmlString))
                return rawXml;

            rawXmlString = Regex.Replace(rawXmlString, @"\$\{(\w+)\}", (m) =>
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
            try
            {
                return GetValue(key);
            }
            catch (Exception e)
            {
                throw new ConfigurationErrorsException($"Error in Configuration Builder '{Name}'::GetValue({key})", e);
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
                throw new ConfigurationErrorsException($"Error in Configuration Builder '{Name}'::GetAllValues({prefix})", e);
            }
        }
    }
}
