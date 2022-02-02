// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    /// <summary>
    /// A ConfigurationProvider that retrieves values from a json config file.
    /// </summary>
    public class SimpleJsonConfigBuilder : KeyValueConfigBuilder
    {
        #pragma warning disable CS1591 // No xml comments for tag literals.
        public const string jsonFileTag = "jsonFile";
        public const string jsonModeTag = "jsonMode";
        public const string keyDelimiter = ":";
        #pragma warning restore CS1591 // No xml comments for tag literals.

        private string _currentSection;
        private Dictionary<string, Dictionary<string, string>> _allSettings;

        /// <summary>
        /// Gets or sets a path to the json file to be read.
        /// </summary>
        public string JsonFile { get; protected set; }
        /// <summary>
        /// Gets or sets the json parsing paradigm to be used by the SimpleJsonConfigBuilder.
        /// </summary>
        public SimpleJsonConfigBuilderMode JsonMode { get; protected set; } = SimpleJsonConfigBuilderMode.Flat; // Flat dictionary, like core secrets.json

        /// <summary>
        /// Initializes the configuration builder lazily.
        /// </summary>
        /// <param name="name">The friendly name of the provider.</param>
        /// <param name="config">A collection of the name/value pairs representing builder-specific attributes specified in the configuration for this provider.</param>
        protected override void LazyInitialize(string name, NameValueCollection config)
        {
            base.LazyInitialize(name, config);

            _allSettings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // JsonFile
            string jsonFile = UpdateConfigSettingWithAppSettings(jsonFileTag);
            if (String.IsNullOrWhiteSpace(jsonFile))
            {
                throw new ArgumentException($"Json file must be specified with the '{jsonFileTag}' attribute.");
            }
            JsonFile = Utils.MapPath(jsonFile, CurrentSection);
            if (!File.Exists(JsonFile))
            {
                if (Optional)
                {
                    // This empty dictionary allows us to effectively no-op any attempt to get values.
                    _allSettings[""] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    return;
                }

                throw new ArgumentException($"Json file does not exist.");
            }

            // JsonMode
            if (UpdateConfigSettingWithAppSettings(jsonModeTag) != null)
            {
                // We want an exception here if 'jsonMode' is specified but unrecognized.
                JsonMode = (SimpleJsonConfigBuilderMode)Enum.Parse(typeof(SimpleJsonConfigBuilderMode), config[jsonModeTag], true);
            }


            // Now load up all the data for easy referencing later
            JObject root;
            using (JsonTextReader jtr = new JsonTextReader(new StreamReader(JsonFile))) {
                root = JObject.Load(jtr);
            }

            if (JsonMode == SimpleJsonConfigBuilderMode.Flat)
            {
                _allSettings[""] = LoadDictionaryFromJObject(root);
            }
            else
            {
                // Non-"sections" get dumped into the default top level dictionary
                _allSettings[""] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                ProcessJson(root, _allSettings[""], "", true);

                // Get separate dictionaries for each "section"
                foreach (JProperty p in root.Properties())
                {
                    if (p.Value.Type == JTokenType.Object)
                    {
                        _allSettings[p.Name] = LoadDictionaryFromJObject(p.Value.Value<JObject>());
                    }
                }
            }
        }

        #pragma warning disable CS1591 // No xml comments for overrides that should not be called directly.
        public override XmlNode ProcessRawXml(XmlNode rawXml)
        {
            if (rawXml != null)
                _currentSection = rawXml.Name;
            return base.ProcessRawXml(rawXml);
        }

        public override ConfigurationSection ProcessConfigurationSection(ConfigurationSection configSection)
        {
            _currentSection = configSection.SectionInformation.Name;
            return base.ProcessConfigurationSection(configSection);
        }
        #pragma warning restore CS1591 // No xml comments for overrides that should not be called directly.

        /// <summary>
        /// Looks up a single 'value' for the given 'key.'
        /// </summary>
        /// <param name="key">The 'key' to look up in the json source. (Prefix handling is not needed here.)</param>
        /// <returns>The value corresponding to the given 'key' or null if no value is found.</returns>
        public override string GetValue(string key)
        {
            string value;
            return GetCurrentDictionary().TryGetValue(key, out value) ? value : null;
        }

        /// <summary>
        /// Retrieves all known key/value pairs from the json source where the key begins with with <paramref name="prefix"/>.
        /// </summary>
        /// <param name="prefix">A prefix string to filter the list of potential keys retrieved from the source.</param>
        /// <returns>A collection of key/value pairs.</returns>
        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            return GetCurrentDictionary().Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private Dictionary<string, string> GetCurrentDictionary()
        {
            if (JsonMode == SimpleJsonConfigBuilderMode.Sectional && _currentSection != null)
            {
                if (_allSettings.TryGetValue(_currentSection, out Dictionary<string, string> d))
                    return d;
            }

            return _allSettings[""];
        }

        private Dictionary<string, string> LoadDictionaryFromJObject(JObject root)
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ProcessJson(root, d, "");
            return d;
        }

        private void ProcessJson(JToken token, Dictionary<string, string> d, string prefix, bool excludeObjects = false)
        {
            switch (token.Type)
            {
                // Objects get flattened
                case JTokenType.Object:
                    if (!excludeObjects)
                    {
                        foreach (JProperty p in token.Value<JObject>().Properties())
                            ProcessJson(p.Value, d, BuildPrefix(prefix, p.Name), excludeObjects);
                    }
                    break;

                // Arrays get expando-flattened
                case JTokenType.Array:
                    JArray array = token.Value<JArray>();
                    for (int i = 0; i < array.Count; i++)
                    {
                        ProcessJson(array[i], d, BuildPrefix(prefix, i.ToString()), excludeObjects);
                    }
                    break;

                // Primatives get stuck in the default section
                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                case JTokenType.Boolean:
                case JTokenType.Bytes:
                case JTokenType.Raw:
                case JTokenType.Null:
                    // Core's json provider throws exceptions on duplicates. Let's use Add() and do the same.
                    d.Add(prefix, token.Value<JValue>().ToString());
                    break;
            }
        }

        private string BuildPrefix(string prefix, string ext)
        {
            if (!String.IsNullOrWhiteSpace(prefix))
                return prefix + keyDelimiter + ext;
            return ext;
        }
    }
}
