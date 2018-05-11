// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See the License.txt file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Xml;

namespace Microsoft.Configuration.ConfigurationBuilders
{
    public class SimpleJsonConfigBuilder : KeyValueConfigBuilder
    {
        public const string jsonFileTag = "jsonFile";
        public const string optionalTag = "optional";
        public const string jsonModeTag = "jsonMode";
        public const string keyDelimiter = ":";

        private string _currentSection;
        private Dictionary<string, Dictionary<string, string>> _allSettings;

        public string JsonFile { get; protected set; }
        public bool Optional { get; protected set; }
        public SimpleJsonConfigBuilderMode JsonMode { get; protected set; } = SimpleJsonConfigBuilderMode.Flat; // Flat dictionary, like core secrets.json

        public override void Initialize(string name, NameValueCollection config)
        {
            base.Initialize(name, config);

            _allSettings = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            // Optional
            bool optional;
            Optional = (Boolean.TryParse(config?[optionalTag], out optional)) ? optional : true;

            // JsonFile
            string jsonFile = config?[jsonFileTag];
            if (String.IsNullOrWhiteSpace(jsonFile))
            {
                throw new ArgumentException($"Json file must be specified with the '{jsonFileTag}' attribute.");
            }
            JsonFile = Utils.MapPath(jsonFile);
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
            if (config != null && config[jsonModeTag] != null)
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

        public override string GetValue(string key)
        {
            string value;
            return GetCurrentDictionary().TryGetValue(key, out value) ? value : null;
        }

        public override ICollection<KeyValuePair<string, string>> GetAllValues(string prefix)
        {
            return GetCurrentDictionary().Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private Dictionary<string, string> GetCurrentDictionary()
        {
            if (JsonMode == SimpleJsonConfigBuilderMode.Sectional && _currentSection != null)
            {
                Dictionary<string, string> d = _allSettings[_currentSection];
                return d ?? _allSettings[""];
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
